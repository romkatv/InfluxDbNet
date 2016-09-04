using Conditions;
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
        // Zero means no downsampling.
        public TimeSpan SamplingPeriod { get; set; }

        // Store at most this many points in memory per key. When the buffer fills up,
        // try to flush. If we are already flushing but there are still to many points,
        // do what OnFull says.
        // Negative means infinity.
        public int MaxPointsPerSeries { get; set; }

        // Send at most this many points per HTTP POST request to InfluxDb.
        // Negative means infinity.
        public int MaxPointsPerBatch { get; set; }

        // See MaxPointsPerSeries.
        public OnFull OnFull { get; set; }

        // Send data to InfluxDb at least once every so often.
        public TimeSpan SendPeriod { get; set; }

        // Timeout for HTTP POST requests to InfluxDb.
        public TimeSpan SendTimeout { get; set; }

        // null is legal.
        public Scheduler Scheduler { get; set; }

        public PublisherConfig Clone()
        {
            return (PublisherConfig)MemberwiseClone();
        }
    }

    class PointBuffer
    {
        readonly PointKey _key;
        // Negative means infinity.
        readonly int _maxSize;
        readonly OnFull _onFull;
        readonly TimeSpan _samplingPeriod;
        readonly Nito.Deque<PointValue> _points = new Nito.Deque<PointValue>();

        public PointBuffer(PointKey key, int maxSize, OnFull onFull, TimeSpan samplingPeriod)
        {
            Condition.Requires(samplingPeriod, "samplingPeriod").IsGreaterOrEqual(TimeSpan.Zero);
            Condition.Requires(key, "key").IsNotNull();
            _key = key;
            _maxSize = maxSize;
            _onFull = onFull;
            _samplingPeriod = samplingPeriod;
        }

        public int Count { get { return _points.Count; } }

        public void Add(PointValue p)
        {
            Condition.Requires(p, "p").IsNotNull();
            int idx = _points.Count;
            // This is essentially insertion sort.
            // Since timestamps usually come in asceding order, it should be fast.
            while (idx > 0 && _points[idx - 1].Timestamp > p.Timestamp) --idx;
            if (idx < _points.Count && _points[idx].Timestamp == p.Timestamp)
            {
                _points[idx].Fields.MergeFrom(p.Fields);
                return;
            }
            if (idx > 0 && idx < _points.Count && UselessMiddle(_points[idx - 1], p, _points[idx]))
            {
                // The point is in between two existing points both of which are within _samplingPeriod.
                return;
            }
            // Can we drop _points[idx - 1]?
            bool uselessLeft = idx > 1 && UselessMiddle(_points[idx - 2], _points[idx - 1], p);
            // Can we drop _points[idx]?
            bool uselessRight = idx + 1 < _points.Count && UselessMiddle(p, _points[idx], _points[idx + 1]);
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
        public void ConsumeAppendOldest(int n, List<Point> target)
        {
            n = n < 0 ? _points.Count : Math.Min(n, _points.Count);
            while (n-- > 0)
            {
                target.Add(new Point() { Key = _key, Value = _points.First() });
                _points.RemoveFromFront();
            }
        }

        void MaybeCompact()
        {
            if (_maxSize < 0 || _points.Count <= _maxSize) return;
            switch (_onFull)
            {
                case OnFull.DropOldest:
                    do { _points.RemoveFromFront(); } while (_points.Count > _maxSize);
                    break;
                default:
                    throw new NotImplementedException("OnFull = " + _onFull);
            }
        }

        bool UselessMiddle(PointValue p1, PointValue p2, PointValue p3)
        {
            return p2.Timestamp - p1.Timestamp < _samplingPeriod &&
                   p3.Timestamp - p2.Timestamp < _samplingPeriod;
        }
    }

    class KeyValueComparer<TKey, TValue> : IEqualityComparer<KeyValuePair<TKey, TValue>>
    {
        public bool Equals(KeyValuePair<TKey, TValue> x, KeyValuePair<TKey, TValue> y)
        {
            return object.Equals(x.Key, y.Key) && object.Equals(x.Value, y.Value);
        }

        public int GetHashCode(KeyValuePair<TKey, TValue> obj)
        {
            return Hash.HashAll(obj.Key, obj.Value);
        }
    }

    class PointKeyComparer : IEqualityComparer<PointKey>
    {
        readonly SequenceComparer<KeyValuePair<string, string>> _cmp =
            new SequenceComparer<KeyValuePair<string, string>>(
                new KeyValueComparer<string, string>());

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
        // Value is true if the entry was added before _batch was cut.
        readonly Dictionary<Reference<Action>, bool> _wake =
            new Dictionary<Reference<Action>, bool>();
        List<Point> _batch = null;

        public Publisher(IBackend backend, PublisherConfig cfg)
        {
            Condition.Requires(backend, "backend").IsNotNull();
            Condition.Requires(cfg, "cfg").IsNotNull();
            _backend = backend;
            _cfg = cfg.Clone();
            if (_cfg.Scheduler == null) _cfg.Scheduler = new Scheduler();
            _send = new PeriodicAction(_cfg.Scheduler, _cfg.SendPeriod, DoFlush);
            _send.Schedule(DateTime.UtcNow + _cfg.SendPeriod);
        }

        public void Push(Point p)
        {
            Condition.Requires(p, "p").IsNotNull();
            Condition.Requires(p.Key, "p.Key").IsNotNull();
            Condition.Requires(p.Value, "p.Value").IsNotNull();
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

        public async Task Flush(TimeSpan timeout)
        {
            using (var cancel = new CancellationTokenSource(timeout))
            {
                Task done = new Task(delegate { }, cancel.Token);
                var wake = new Reference<Action>(() => done.RunSynchronously());
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
            var empty = new List<PointKey>();
            foreach (var kv in _points)
            {
                kv.Value.ConsumeAppendOldest(
                    _cfg.MaxPointsPerBatch < 0 ? -1 : _cfg.MaxPointsPerBatch - _batch.Count, _batch);
                if (kv.Value.Count == 0) empty.Add(kv.Key);
            }
            foreach (PointKey key in empty) _points.Remove(key);
            foreach (var key in _wake.Keys.ToList()) _wake[key] = true;
        }

        bool IsFull()
        {
            Condition.Requires(Monitor.IsEntered(_monitor)).IsTrue();
            return _cfg.MaxPointsPerBatch >= 0 && _points.Count >= _cfg.MaxPointsPerBatch;
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
            await _backend.Send(_batch, _cfg.SendTimeout);
            lock (_monitor)
            {
                _batch = null;
                foreach (var kv in _wake.ToList())
                {
                    if (kv.Value)
                    {
                        kv.Key.Value.Invoke();
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
