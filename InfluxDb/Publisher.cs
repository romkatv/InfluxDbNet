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
        // Negative means infinity.
        public int MaxStoredPoints { get; set; }
        // Negative means infinity.
        public int MaxPointsPerBatch { get; set; }
        public OnFull OnFull { get; set; }
        public TimeSpan SendPeriod { get; set; }
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
        // Negative means infinity.
        readonly int _maxSize;
        readonly OnFull _onFull;
        readonly Nito.Deque<Point> _points = new Nito.Deque<Point>();

        public PointBuffer(int maxSize, OnFull onFull)
        {
            _maxSize = maxSize;
            _onFull = onFull;
        }

        public int Count { get { return _points.Count; } }

        public void Add(Point p)
        {
            int idx = _points.Count;
            // This is essentially insertion sort.
            // Since timestamps usually come in asceding order, it should be fast.
            while (idx > 0 && _points[idx - 1].Timestamp > p.Timestamp) --idx;
            _points.Insert(idx, p);
            MaybeCompact();
        }

        // Negative means infinity.
        public List<Point> ConsumeOldest(int n)
        {
            n = n < 0 ? _points.Count : Math.Min(n, _points.Count);
            var res = new List<Point>(n);
            res.AddRange(_points.Take(n));
            _points.RemoveRange(0, n);
            return res;
        }

        void MaybeCompact()
        {
            if (_maxSize < 0) return;
            while (_points.Count > _maxSize)
            {
                switch (_onFull)
                {
                    case OnFull.DropOldest:
                        _points.RemoveFromFront();
                        break;
                    default:
                        throw new NotImplementedException("OnFull = " + _onFull);
                }
            }
        }
    }

    public class Publisher : ISink, IDisposable
    {
        readonly object _monitor = new object();
        readonly IBackend _backend;
        readonly PublisherConfig _cfg;
        readonly PointBuffer _points;
        readonly PeriodicAction _send;
        // Value is true if the entry was added before _batch was cut.
        readonly Dictionary<Reference<Action>, bool> _wake = new Dictionary<Reference<Action>, bool>();
        List<Point> _batch = null;

        public Publisher(IBackend backend, PublisherConfig cfg)
        {
            Condition.Requires(backend, "backend").IsNotNull();
            Condition.Requires(cfg, "cfg").IsNotNull();
            _backend = backend;
            _cfg = cfg.Clone();
            if (_cfg.Scheduler == null) _cfg.Scheduler = new Scheduler();
            _points = new PointBuffer(_cfg.MaxStoredPoints, _cfg.OnFull);
            _send = new PeriodicAction(_cfg.Scheduler, _cfg.SendPeriod, DoFlush);
            _send.Schedule(DateTime.UtcNow + _cfg.SendPeriod);
        }

        public void Write(Point p)
        {
            lock (_monitor)
            {
                _points.Add(p);
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
            _batch = _points.ConsumeOldest(_cfg.MaxPointsPerBatch);
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
