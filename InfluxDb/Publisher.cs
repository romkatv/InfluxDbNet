using Conditions;
using Nito.Collections;
using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace InfluxDb {
  public enum OnFull {
    DropOldest,
    Block,
    // Other options to consider: Downsample.
  }

  public class PublisherConfig {
    // Downsample data on the fly by dropping points with the same key
    // that are close to each other in time.
    // Must be non-negative. Zero means no downsampling.
    public TimeSpan SamplingPeriod { get; set; }

    // Store at most this many points in memory per key. When the buffer fills up,
    // try to flush. If we are already flushing but there are still too many points,
    // do what OnFull says.
    // Negative means infinity. Zero and one are invalid.
    public int MaxPointsPerSeries { get; set; }

    // Send at most this many points per HTTP POST request to InfluxDb.
    // Negative means infinity. Zero is invalid.
    public int MaxPointsPerBatch { get; set; }

    // See MaxPointsPerSeries.
    public OnFull OnFull { get; set; }

    // Send data to InfluxDb at least once every so often (without overlapping requests, though).
    // Must be positive.
    public TimeSpan SendPeriod { get; set; }

    // Timeout for HTTP POST requests to InfluxDb. TimeSpan.FromMilliseconds(-1) means infinity.
    public TimeSpan SendTimeout { get; set; }

    // Run all delayed and periodic actions on this scheduler. Leave it as null if you don't care.
    public Scheduler Scheduler { get; set; }

    public PublisherConfig Clone() {
      return (PublisherConfig)MemberwiseClone();
    }
  }

  struct Synchronized<T> {
    public Synchronized(T value) {
      Monitor = new object();
      Value = value;
    }

    public object Monitor { get; }
    public T Value { get; }
  }

  class PointBuffer {
    static readonly Logger _log = LogManager.GetCurrentClassLogger();

    readonly PointKey _key;
    // Negative means infinity.
    readonly int _maxSize;
    readonly OnFull _onFull;
    // Non-negative.
    readonly TimeSpan _samplingPeriod;
    // Invariant: _maxSize < 0 || _points.Count <= _maxSize.
    readonly Deque<PointValue> _points = new Deque<PointValue>();
    // The last consumed point. Not null.
    PointValue _bottom = new PointValue();
    // Has _bottom been modified?
    bool _bottomDirty = false;
    ulong _version = 0;

    // Does not mutate `key`.
    public PointBuffer(PointKey key, int maxSize, OnFull onFull, TimeSpan samplingPeriod) {
      Condition.Requires(maxSize, nameof(maxSize)).IsNotInRange(0, 1);
      Condition.Requires(samplingPeriod, nameof(samplingPeriod)).IsGreaterOrEqual(TimeSpan.Zero);
      _key = key;
      _maxSize = maxSize;
      _onFull = onFull;
      _samplingPeriod = samplingPeriod;
    }

    public int Count { get { return _points.Count + (_bottomDirty ? 1 : 0); } }
    public bool Full { get { return _maxSize >= 0 && 2 * Count >= _maxSize; } }
    public bool Overfull { get { return _maxSize >= 0 && Count > _maxSize; } }

    // Does not mutate `f`.
    public void Add(ShardedPoint p) {
      DateTime t = Timestamp(p);
      int idx = _points.Count;
      ++_version;

      // Handle the most common case first. This is just an optimization. This block doesn't
      // affect visible behavior.
      if (idx > 0 && t >= _points[idx - 1].Timestamp && t < _points[idx - 1].Timestamp + _samplingPeriod) {
        _points[idx - 1].MergeWith(p, t, _version);
        return;
      }

      if (t <= _bottom.Timestamp) {
        _bottom.MergeWith(p, t, _version);
        _bottomDirty = true;
        MaybeCompact(p.Pools);
        return;
      }

      // This is essentially insertion sort.
      // Since timestamps usually come in asceding order, it should be fast.
      while (idx > 0 && _points[idx - 1].Timestamp > t) --idx;
      if (idx > 0 && _points[idx - 1].Timestamp == t) {
        _points[idx - 1].MergeWith(p, t, _version);
        return;
      }

      if (idx > 0 && idx < _points.Count && UselessMiddle(_points[idx - 1].Timestamp, t, _points[idx].Timestamp)) {
        // The point is in between two existing points both of which are within _samplingPeriod.
        _points[idx].MergeWith(p, t, _version);
        return;
      }

      // Can we drop _points[idx - 1]?
      bool uselessLeft = idx > 1 &&
          UselessMiddle(_points[idx - 2].Timestamp, _points[idx - 1].Timestamp, t);
      // Can we drop _points[idx]?
      bool uselessRight = idx + 1 < _points.Count &&
          UselessMiddle(t, _points[idx].Timestamp, _points[idx + 1].Timestamp);
      if (uselessLeft && uselessRight) {
        _points[idx - 1].MergeWith(p, t, _version);
        _points[idx].MergeWithNewer(_points[idx + 1], p.Pools);
        _points.RemoveAt(idx + 1);
      } else if (uselessLeft) {
        _points[idx - 1].MergeWith(p, t, _version);
      } else if (uselessRight) {
        _points[idx].MergeWithNewer(_points[idx + 1], p.Pools);
        _points[idx + 1] = _points[idx];
        _points[idx] = new PointValue();
        _points[idx].MergeWith(p, t, _version);
      } else {
        _points.Insert(idx, new PointValue());
        _points[idx].MergeWith(p, t, _version);
        MaybeCompact(p.Pools);
      }
    }

    // Negative means infinity.
    // The caller must not mutate Point.Key.
    public void ConsumeAppendOldest(int n, List<Point> target) {
      n = n < 0 ? Count : Math.Min(n, Count);
      if (n == 0) return;
      if (_bottomDirty) {
        target.Add(new Point() { Key = _key, Value = _bottom });
        _bottomDirty = false;
        --n;
      }
      while (n-- > 0) {
        target.Add(new Point() { Key = _key, Value = _points.RemoveFromFront() });
      }
      _bottom = target.Last().Value.Clone(new Pools());
    }

    void MaybeCompact(Pools pools) {
      if (!Overfull) return;
      switch (_onFull) {
        case OnFull.DropOldest:
          _log.LogEvery(TimeSpan.FromSeconds(10), LogLevel.Warn,
                        "Dropping statistics on the floor. Check your InfluxDb configuration.");
          do {
            PointValue p = _points.RemoveFromFront();
            // The constructor guarantees that _maxSize > 1. Hence _points can't be empty.
            p.MergeWithNewer(_points[0], pools);
            _points[0] = p;
          } while (Overfull);
          break;
        case OnFull.Block:
          // The blocking will be done by Publisher.
          return;
        default:
          throw new NotImplementedException("OnFull = " + _onFull);
      }
    }

    // Can we drop p2?
    // Requires: p1.Timestamp <= p2.Timestamp <= p3.Timestamp.
    bool UselessMiddle(DateTime t1, DateTime t2, DateTime t3) {
      return t2 - t1 < _samplingPeriod && t3 - t2 < _samplingPeriod;
    }

    static DateTime Timestamp(ShardedPoint p) {
      if (p.Final.Timestamp.HasValue) return p.Final.Timestamp.Value;
      for (PartialPoint node = p.Local.Last; node != null; node = node.Prev) {
        if (node.Timestamp.HasValue) return node.Timestamp.Value;
      }
      return p.Global.Timestamp ?? DateTime.UtcNow;
    }
  }

  public class Publisher : ISink, IDisposable {
    static readonly Logger _log = LogManager.GetCurrentClassLogger();

    readonly object _monitor = new object();
    readonly IBackend _backend;
    readonly PublisherConfig _cfg;
    readonly ConcurrentDictionary<PointKey, Synchronized<PointBuffer>> _points =
        new ConcurrentDictionary<PointKey, Synchronized<PointBuffer>>();
    readonly PeriodicAction _send;
    // Keys are actions that complete Flush() tasks.
    // Values are true for entries that were added before _batch was cut and the batch consumed all points.
    readonly Dictionary<Reference<Action>, bool> _wake =
        new Dictionary<Reference<Action>, bool>();
    // Points ready to be sent to the backend. If not null, we'll be trying
    // to send them until we succeed. Only accessed from the scheduler thread.
    List<Point> _batch = null;

    public Publisher(IBackend backend, PublisherConfig cfg) {
      Condition.Requires(backend, nameof(backend)).IsNotNull();
      Condition.Requires(cfg, nameof(cfg)).IsNotNull();
      Condition.Requires(cfg.MaxPointsPerBatch, nameof(cfg.MaxPointsPerBatch)).IsNotEqualTo(0);
      Condition.Requires(cfg.SendPeriod, nameof(cfg.SendPeriod)).IsGreaterThan(TimeSpan.Zero);
      if (cfg.SendTimeout != TimeSpan.FromMilliseconds(-1))
        Condition.Requires(cfg.SendTimeout, nameof(cfg.SendTimeout)).IsGreaterOrEqual(TimeSpan.Zero);
      Condition.Requires(cfg.SamplingPeriod, nameof(cfg.SamplingPeriod)).IsGreaterOrEqual(TimeSpan.Zero);
      _backend = backend;
      _cfg = cfg.Clone();
      if (_cfg.Scheduler == null) _cfg.Scheduler = new Scheduler();
      _send = new PeriodicAction(_cfg.Scheduler, _cfg.SendPeriod, DoFlush);
      _send.Schedule(DateTime.UtcNow + _cfg.SendPeriod);
    }

    // Requires: There are no duplicate indices in fields and tags.
    public void Push(string name, ShardedPoint p) {
      var key = new PointKey() { Name = name, Tags = p };
      if (!_points.TryGetValue(key, out Synchronized<PointBuffer> buf)) {
        key.Compact();
        buf = new Synchronized<PointBuffer>(
            new PointBuffer(key, _cfg.MaxPointsPerSeries, _cfg.OnFull, _cfg.SamplingPeriod));
        buf = _points.GetOrAdd(key, buf);
      }

      bool becameFull;
      bool overfull;
      lock (buf.Monitor) {
        bool wasFull = buf.Value.Full;
        buf.Value.Add(p);
        becameFull = !wasFull && buf.Value.Full;
        overfull = buf.Value.Overfull;
      }

      if (becameFull) _send.Schedule(DateTime.UtcNow);
      if (overfull && _cfg.OnFull == OnFull.Block) {
        lock (buf.Monitor) {
          while (buf.Value.Overfull) Monitor.Wait(_monitor);
        }
      }
    }

    // It's unspecified whether it'll wait for the points pushed after the call to Flush().
    public async Task Flush(TimeSpan timeout) {
      using (var cancel = new CancellationTokenSource(timeout)) {
        Task done = new Task(delegate { }, cancel.Token);
        var wake = new Reference<Action>(() => {
          try { done.Start(); }  // it's gonna throw if the task has already been cancelled
          catch (InvalidOperationException) { }
        });
        lock (_monitor) _wake[wake] = false;
        _send.Schedule(DateTime.UtcNow);
        try {
          await done;
        } finally {
          lock (_monitor) _wake.Remove(wake);
        }
      }
    }

    public void Dispose() {
      _send.Dispose();
    }

    // Only called from the scheduler thread.
    //
    // Guarantees: _batch != null.
    void MaybeMakeBatch() {
      if (_batch != null) return;

      DateTime start = DateTime.UtcNow;
      int numBuffers = 0;
      int numEmptyBuffers = 0;

      _batch = new List<Point>();
      bool tookAll = true;
      List<Reference<Action>> wake;
      lock (_monitor) wake = _wake.Keys.ToList();
      foreach (var kv in _points) {
        ++numBuffers;
        lock (kv.Value.Monitor) {
          PointBuffer buf = kv.Value.Value;
          if (buf.Count == 0) {
            ++numEmptyBuffers;
            continue;
          }
          bool wasOverfull = buf.Overfull;
          buf.ConsumeAppendOldest(_cfg.MaxPointsPerBatch < 0 ? -1 : _cfg.MaxPointsPerBatch - _batch.Count, _batch);
          if (buf.Count > 0) tookAll = false;
          if (wasOverfull && !buf.Overfull) Monitor.PulseAll(kv.Value.Monitor);
        }
      }
      if (tookAll) {
        lock (_monitor) {
          foreach (var key in wake) {
            if (_wake.ContainsKey(key)) _wake[key] = true;
          }
        }
      }
      _log.Debug(
          "Batch stats: time to build = {0:N2} ms; number of buffers = {1}; number of empty buffers = {2}; number of points = {3}",
          (DateTime.UtcNow - start).TotalMilliseconds, numBuffers, numEmptyBuffers, _batch.Count);
    }

    // At most one instance of DoFlush() is running at any given time.
    async Task DoFlush() {
      MaybeMakeBatch();
      // It's OK to read _batch without a lock here. It can't be modified by any other
      // thread when it's not null.
      if (_batch.Count > 0) {
        try {
          await _backend.Send(_batch, _cfg.SendTimeout);
        } catch (Exception e) {
          _log.Warn(e, "Unable to publish {0} points. Will retry in {1}.", _batch.Count, _cfg.SendPeriod);
          return;
        }
      }
      _batch = null;
      bool mustFlush;
      lock (_monitor) {
        foreach (var kv in _wake.ToList()) {
          if (kv.Value) {
            Task.Run(kv.Key.Value);
            _wake.Remove(kv.Key);
          }
        }
        mustFlush = _wake.Count > 0;
      }
      if (!mustFlush && (_cfg.MaxPointsPerBatch > 0 || _cfg.MaxPointsPerSeries > 0)) {
        long numPoints = 0;
        foreach (Synchronized<PointBuffer> buf in _points.Values) {
          lock (buf.Monitor) {
            if (_cfg.MaxPointsPerBatch > 0) {
              numPoints += buf.Value.Count;
              if (numPoints >= _cfg.MaxPointsPerBatch) {
                mustFlush = true;
                break;
              }
            }
            if (_cfg.MaxPointsPerSeries > 0 && buf.Value.Full) {
              mustFlush = true;
              break;
            }
          }
        }
      }
      if (mustFlush) {
        MaybeMakeBatch();
        _send.Schedule(DateTime.UtcNow);
      }
    }
  }
}
