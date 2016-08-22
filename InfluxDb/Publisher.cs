﻿using Conditions;
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
        // Zero means no downsampling.
        public TimeSpan SamplingPeriod { get; set; }
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
        readonly TimeSpan _samplingPeriod;
        readonly Nito.Deque<Point> _points = new Nito.Deque<Point>();

        public PointBuffer(int maxSize, OnFull onFull, TimeSpan samplingPeriod)
        {
            Condition.Requires(samplingPeriod, "samplingPeriod").IsGreaterOrEqual(TimeSpan.Zero);
            _maxSize = maxSize;
            _onFull = onFull;
            _samplingPeriod = samplingPeriod;
        }

        public int Count { get { return _points.Count; } }

        public void Add(Point p)
        {
            int idx = _points.Count;
            // This is essentially insertion sort.
            // Since timestamps usually come in asceding order, it should be fast.
            while (idx > 0 && _points[idx - 1].Timestamp > p.Timestamp) --idx;
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
                target.Add(_points.First());
                _points.RemoveFromFront();
            }
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

        bool UselessMiddle(Point p1, Point p2, Point p3)
        {
            return p2.Timestamp - p1.Timestamp < _samplingPeriod &&
                   p3.Timestamp - p2.Timestamp < _samplingPeriod;
        }
    }

    public class Publisher : ISink, IDisposable
    {
        readonly object _monitor = new object();
        readonly IBackend _backend;
        readonly PublisherConfig _cfg;
        readonly Dictionary<string, PointBuffer> _points = new Dictionary<string, PointBuffer>();
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
            _send = new PeriodicAction(_cfg.Scheduler, _cfg.SendPeriod, DoFlush);
            _send.Schedule(DateTime.UtcNow + _cfg.SendPeriod);
        }

        public void Write(Point p)
        {
            lock (_monitor)
            {
                PointBuffer buf;
                if (!_points.TryGetValue(p.Name, out buf))
                {
                    buf = new PointBuffer(_cfg.MaxStoredPoints, _cfg.OnFull, _cfg.SamplingPeriod);
                    _points.Add(p.Name, buf);
                }
                buf.Add(p);
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
            foreach (PointBuffer buf in _points.Values)
            {
                buf.ConsumeAppendOldest(
                    _cfg.MaxPointsPerBatch < 0 ? -1 : _cfg.MaxPointsPerBatch - _batch.Count, _batch);
            }
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
