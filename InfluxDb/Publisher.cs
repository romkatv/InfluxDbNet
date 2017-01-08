using Conditions;
using NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace InfluxDb
{
    public enum OnFull
    {
        DropOldest,
        // Other options to consider: Downsample, Block.
    }

    public class PublisherConfig
    {
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

        public PublisherConfig Clone()
        {
            return (PublisherConfig)MemberwiseClone();
        }
    }

    class PointBuffer
    {
        static readonly Logger _log = LogManager.GetCurrentClassLogger();

        readonly PointKey _key;
        // Negative means infinity.
        readonly int _maxSize;
        readonly OnFull _onFull;
        // Non-negative.
        readonly TimeSpan _samplingPeriod;
        // Invariant: _maxSize < 0 || _points.Count <= _maxSize.
        readonly Nito.Deque<PointValue> _points = new Nito.Deque<PointValue>();
        // The last consumed point. Not null.
        PointValue _bottom = new PointValue() { Timestamp = new DateTime(), Fields = new Dictionary<string, Field>() };
        // Has _bottom been modified?
        bool _bottomDirty = false;

        // The key is immutable: neither PointBuffer nor the caller may change it.
        public PointBuffer(PointKey key, int maxSize, OnFull onFull, TimeSpan samplingPeriod)
        {
            Condition.Requires(key, "key").IsNotNull();
            Condition.Requires(maxSize, "maxSize").IsNotInRange(0, 1);
            Condition.Requires(samplingPeriod, "samplingPeriod").IsGreaterOrEqual(TimeSpan.Zero);
            _key = key;
            _maxSize = maxSize;
            _onFull = onFull;
            _samplingPeriod = samplingPeriod;
        }

        // Guarantees: maxSize < 0 || Count <= maxSize (maxSize is the constructor argument).
        public int Count { get { return _points.Count + (_bottomDirty ? 1 : 0); } }

        // The caller must not mutate `p`. PointBuffer may mutate it.
        public void Add(PointValue p)
        {
            Condition.Requires(p, "p").IsNotNull();
            if (p.Timestamp <= _bottom.Timestamp)
            {
                if (p.Timestamp == _bottom.Timestamp)
                {
                    var tmp = p;
                    p = _bottom;
                    _bottom = tmp;
                }
                _bottom.MergeWithOlder(p);
                _bottomDirty = true;
                MaybeCompact();
                return;
            }
            int idx = _points.Count;
            // This is essentially insertion sort.
            // Since timestamps usually come in asceding order, it should be fast.
            while (idx > 0 && _points[idx - 1].Timestamp > p.Timestamp) --idx;
            if (idx > 0 && _points[idx - 1].Timestamp == p.Timestamp)
            {
                p.MergeWithOlder(_points[idx - 1]);
                _points[idx - 1] = p;
                return;
            }
            if (idx > 0 && idx < _points.Count && TryAggregate(_points[idx - 1], p, _points[idx]))
            {
                // The point is in between two existing points both of which are within _samplingPeriod.
                return;
            }
            // Can we drop _points[idx - 1]?
            bool uselessLeft = idx > 1 && TryAggregate(_points[idx - 2], _points[idx - 1], p);
            // Can we drop _points[idx]?
            bool uselessRight = idx + 1 < _points.Count && TryAggregate(p, _points[idx], _points[idx + 1]);
            if (uselessLeft && uselessRight)
            {
                _points.RemoveAt(idx);
                _points[idx - 1] = p;
            }
            else if (uselessLeft)
            {
                _points[idx - 1] = p;
            }
            else if (uselessRight)
            {
                _points[idx] = p;
            }
            else
            {
                _points.Insert(idx, p);
                MaybeCompact();
            }
        }

        // Negative means infinity.
        // The caller must not mutate Point.Key.
        public void ConsumeAppendOldest(int n, List<Point> target)
        {
            n = n < 0 ? Count : Math.Min(n, Count);
            if (n == 0) return;
            if (_bottomDirty)
            {
                target.Add(new Point() { Key = _key, Value = _bottom });
                _bottomDirty = false;
                --n;
            }
            while (n-- > 0)
            {
                target.Add(new Point() { Key = _key, Value = _points.RemoveFromFront() });
            }
            _bottom = target.Last().Value.Clone();
        }

        void MaybeCompact()
        {
            if (_maxSize < 0 || Count <= _maxSize) return;
            _log.LogEvery(TimeSpan.FromSeconds(10), LogLevel.Warn,
                          "Dropping statistics on the floor. Check your InfluxDb configuration.");
            switch (_onFull)
            {
                case OnFull.DropOldest:
                    do
                    {
                        PointValue p = _points.RemoveFromFront();
                        // The constructor guarantees that _maxSize > 1. Hence _points can't be empty.
                        _points[0].MergeWithOlder(p);
                    } while (Count > _maxSize);
                    break;
                default:
                    throw new NotImplementedException("OnFull = " + _onFull);
            }
        }

        // If possible, merge p2 into p3. Then p2 can be dropped.
        // Requires: p1.Timestamp <= p2.Timestamp <= p3.Timestamp.
        bool TryAggregate(PointValue p1, PointValue p2, PointValue p3)
        {
            if (!UselessMiddle(p1, p2, p3)) return false;
            p3.MergeWithOlder(p2);
            return true;
        }

        // Can we drop p2?
        // Requires: p1.Timestamp <= p2.Timestamp <= p3.Timestamp.
        bool UselessMiddle(PointValue p1, PointValue p2, PointValue p3)
        {
            return p2.Timestamp - p1.Timestamp < _samplingPeriod &&
                   p3.Timestamp - p2.Timestamp < _samplingPeriod;
        }
    }

    class PointKeyComparer : IEqualityComparer<PointKey>
    {
        readonly DictionaryComparer<string, string> _cmp =
            new DictionaryComparer<string, string>();

        public bool Equals(PointKey x, PointKey y)
        {
            if (x == null) return y == null;
            if (y == null) return false;
            return x.Name == y.Name && _cmp.Equals(x.Tags, y.Tags);
        }

        public int GetHashCode(PointKey p)
        {
            if (p == null) return 501107580;  // Random number.
            return Hash.Combine(Hash.HashAll(p.Name), _cmp.GetHashCode(p.Tags));
        }
    }

    public class Publisher : ISink, IDisposable
    {
        readonly object _monitor = new object();
        readonly IBackend _backend;
        readonly PublisherConfig _cfg;
        readonly Dictionary<PointKey, PointBuffer> _points =
            new Dictionary<PointKey, PointBuffer>(new PointKeyComparer());
        readonly PeriodicAction _send;
        // Keys are actions that complete Flush() tasks.
        // Values are true for entries that were added before _batch was cut and the batch consumed all points.
        readonly Dictionary<Reference<Action>, bool> _wake =
            new Dictionary<Reference<Action>, bool>();
        // Points ready to be sent to the backend. If not null, we'll be trying
        // to send them until we succeed.
        List<Point> _batch = null;

        public Publisher(IBackend backend, PublisherConfig cfg)
        {
            Condition.Requires(backend, "backend").IsNotNull();
            Condition.Requires(cfg, "cfg").IsNotNull();
            Condition.Requires(cfg.MaxPointsPerBatch, "cfg.MaxPointsPerBatch").IsNotEqualTo(0);
            Condition.Requires(cfg.SendPeriod, "cfg.SendPeriod").IsGreaterThan(TimeSpan.Zero);
            if (cfg.SendTimeout != TimeSpan.FromMilliseconds(-1))
                Condition.Requires(cfg.SendTimeout, "cfg.SendTimeout").IsGreaterOrEqual(TimeSpan.Zero);
            Condition.Requires(cfg.SamplingPeriod, "cfg.SamplingPeriod").IsGreaterOrEqual(TimeSpan.Zero);
            _backend = backend;
            _cfg = cfg.Clone();
            if (_cfg.Scheduler == null) _cfg.Scheduler = new Scheduler();
            _send = new PeriodicAction(_cfg.Scheduler, _cfg.SendPeriod, DoFlush);
            _send.Schedule(DateTime.UtcNow + _cfg.SendPeriod);
        }

        // The caller must not mutate `p`. Publisher may mutate it.
        public void Push(Point p)
        {
            Condition.Requires(p, "p").IsNotNull();
            Condition.Requires(p.Key, "p.Key").IsNotNull();
            Condition.Requires(p.Value, "p.Value").IsNotNull();
            Condition.Requires(p.Key.Name, "p.Key.Name").IsNotNull();
            Condition.Requires(p.Key.Tags, "p.Key.Tags").IsNotNull();
            Condition.Requires(p.Value.Fields, "p.Value.Fields").IsNotNull();
            Condition.Requires(p.Value.Fields, "p.Value.Fields").IsNotEmpty();
            lock (_monitor)
            {
                PointBuffer buf;
                if (!_points.TryGetValue(p.Key, out buf))
                {
                    buf = new PointBuffer(p.Key, _cfg.MaxPointsPerSeries, _cfg.OnFull, _cfg.SamplingPeriod);
                    _points.Add(p.Key, buf);
                }
                buf.Add(p.Value);
                if (_batch == null && IsFull())
                {
                    MakeBatch();
                    _send.Schedule(DateTime.UtcNow);
                }
            }
        }

        // It's unspecified whether it'll wait for the points pushed after the call to Flush().
        public async Task Flush(TimeSpan timeout)
        {
            using (var cancel = new CancellationTokenSource(timeout))
            {
                Task done = new Task(delegate { }, cancel.Token);
                var wake = new Reference<Action>(() =>
                {
                    try { done.RunSynchronously(); }  // it's gonna throw if the task has already been cancelled
                    catch { }
                });
                lock (_monitor) _wake[wake] = false;
                _send.Schedule(DateTime.UtcNow);
                try
                {
                    await done;
                }
                finally
                {
                    lock (_monitor) _wake.Remove(wake);
                }
            }
        }

        public void Dispose()
        {
            _send.Dispose();
        }

        void MakeBatch()
        {
            Condition.Requires(_batch, "_batch").IsNull();
            Condition.Requires(Monitor.IsEntered(_monitor)).IsTrue();
            _batch = new List<Point>();
            foreach (var kv in _points)
            {
                kv.Value.ConsumeAppendOldest(
                    _cfg.MaxPointsPerBatch < 0 ? -1 : _cfg.MaxPointsPerBatch - _batch.Count, _batch);
            }
            if (_points.Values.All((buf) => buf.Count == 0))
            {
                foreach (var key in _wake.Keys.ToList()) _wake[key] = true;
            }
        }

        bool IsFull()
        {
            Condition.Requires(Monitor.IsEntered(_monitor)).IsTrue();
            return (_cfg.MaxPointsPerBatch >= 0 &&
                    _points.Values.Select(b => b.Count).Sum() >= _cfg.MaxPointsPerBatch) ||
                   (_cfg.MaxPointsPerSeries >= 0 &&
                    _points.Values.Any(b => 2 * b.Count > _cfg.MaxPointsPerSeries));
        }

        // At most one instance of DoFlush() is running at any given time.
        async Task DoFlush()
        {
            lock (_monitor)
            {
                if (_batch == null) MakeBatch();
            }
            // If we can't send, this statement will throw and we'll retry in SendPeriod.
            // It's OK to read _batch without a lock here. It can't be modified by any other
            // thread when it's not null.
            if (_batch.Count > 0) await _backend.Send(_batch, _cfg.SendTimeout);
            lock (_monitor)
            {
                _batch = null;
                foreach (var kv in _wake.ToList())
                {
                    if (kv.Value)
                    {
                        Task.Run(kv.Key.Value);
                        _wake.Remove(kv.Key);
                    }
                }
                if (IsFull() || _wake.Count > 0)
                {
                    MakeBatch();
                    _send.Schedule(DateTime.UtcNow);
                }
            }
        }
    }
}
