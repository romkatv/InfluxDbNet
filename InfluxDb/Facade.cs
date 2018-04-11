using Conditions;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using Tags = System.Collections.Generic.Dictionary<string, string>;
using Fields = System.Collections.Generic.Dictionary<string, InfluxDb.Field>;

namespace InfluxDb
{
    public class Facade
    {
        static volatile Facade _instance;

        readonly ThreadLocal<Overrides> _overrides = new ThreadLocal<Overrides>(() => new Overrides());
        readonly ISink _sink;

        public Facade(ISink sink)
        {
            Condition.Requires(sink, nameof(sink)).IsNotNull();
            _sink = sink;
        }

        // Name can be null, in which case it is extracted from `TColumns`.
        public void Push<TColumns>(string name, DateTime t, TColumns cols)
        {
            Override x = _overrides.Value.AggregateCopy();
            Extract(cols, x);
            x.Timestamp = t;
            DoPush(name ?? MeasurementExtractor<TColumns>.Name, x);
        }

        // Name can be null, in which case it is extracted from `TColumns`.
        public void Push<TColumns>(string name, TColumns cols)
        {
            Override x = _overrides.Value.AggregateCopy();
            Extract(cols, x);
            DoPush(name ?? MeasurementExtractor<TColumns>.Name, x);
        }

        public void Push<TColumns>(DateTime t, TColumns cols) => Push(null, t, cols);

        public void Push<TColumns>(TColumns cols) => Push(null, cols);

        // Name and point cannot be null.
        public void Push(string name, DateTime t, PartialPoint p)
        {
            Condition.Requires(p, nameof(p)).IsNotNull();
            Override x = _overrides.Value.AggregateCopy();
            x.MergeFrom(t, p);
            DoPush(name, x);
        }

        public void Push(string name, PartialPoint p)
        {
            Condition.Requires(p, nameof(p)).IsNotNull();
            Override x = _overrides.Value.AggregateCopy();
            x.MergeFrom(null, p);
            DoPush(name, x);
        }

        // Dispose() on the returned object must be called on the same thread.
        public IDisposable At(DateTime t) => _overrides.Value.Add(new Override() { Timestamp = t });

        // Dispose() on the returned object must be called on the same thread.
        public IDisposable With<TColumns>(DateTime t, TColumns cols)
        {
            var data = new Override() { Timestamp = t };
            Extract(cols, data);
            return _overrides.Value.Add(data);
        }

        // Dispose() on the returned object must be called on the same thread.
        public IDisposable With<TColumns>(TColumns cols)
        {
            var data = new Override();
            Extract(cols, data);
            return _overrides.Value.Add(data);
        }

        // Dispose() on the returned object must be called on the same thread.
        public IDisposable With(DateTime t, PartialPoint p)
        {
            Condition.Requires(p, nameof(p)).IsNotNull();
            var x = new Override();
            x.MergeFrom(t, p);
            return _overrides.Value.Add(x);
        }

        // Dispose() on the returned object must be called on the same thread.
        public IDisposable With(PartialPoint p)
        {
            Condition.Requires(p, nameof(p)).IsNotNull();
            var x = new Override();
            x.MergeFrom(null, p);
            return _overrides.Value.Add(x);
        }

        // DoPush() stores a reference to `p` and may mutate it at any point in the future.
        // Thus, the caller must not access `p` or any of its subobjects after DoPush() returns.
        void DoPush(string name, Override p)
        {
            Condition.Requires(name, nameof(name)).IsNotNullOrWhiteSpace();
            Condition.Requires(p, nameof(p)).IsNotNull();
            if (p.Fields?.Count > 0)
            {
                // The sink may mutate the point. We must not mutate it.
                _sink.Push(new Point()
                {
                    Key = new PointKey()
                    {
                        Name = name,
                        Tags = p.Tags ?? new Tags()
                    },
                    Value = new PointValue()
                    {
                        Timestamp = p.Timestamp ?? DateTime.UtcNow,
                        Fields = p.Fields
                    },
                });
            }
        }

        public static Facade Instance
        {
            get { return _instance; }
            set { _instance = value; }
        }

        // Sets Point.Key.Timestamp to DateTime.MinValue, which has a special meaning. Push() replaces
        // such values with DateTime.UtcNow.
        public static PartialPoint Extract<TColumns>(TColumns cols)
        {
            var p = new PartialPoint();
            Extract(cols, p);
            return p;
        }

        // Copies tags and fields (but not the name) from `cols` to `p`.
        static void Extract<TColumns>(TColumns cols, PartialPoint p)
        {
            MemberExtractor<TColumns>.Instance.Extract
            (
                cols,
                (key, val) =>
                {
                    p.Tags = p.Tags ?? new Tags();
                    p.Tags[key] = val;  // the last value wins
                },
                (key, val) =>
                {
                    p.Fields = p.Fields ?? new Fields();
                    p.Fields[key] = val;  // the last value wins
                }
            );
        }
    }

    class Override : PartialPoint
    {
        // If null, doesn't override timestamp.
        // DateTime.MinValue is special. Such values get replaced with DateTime.UtcNow in Facade.Push().
        public DateTime? Timestamp { get; set; }

        public void MergeFrom(Override other) => MergeFrom(other?.Timestamp, other);

        public void MergeFrom(DateTime? t, PartialPoint p)
        {
            base.MergeFrom(p);
            Timestamp = t ?? Timestamp;
        }
    }

    class Overrides
    {
        readonly LinkedList<Override> _data = new LinkedList<Override>();

        class Deleter : IDisposable
        {
            readonly int _thread;
            readonly Overrides _outer;
            readonly LinkedListNode<Override> _data;

            public Deleter(Overrides outer, LinkedListNode<Override> data)
            {
                Condition.Requires(outer, nameof(outer)).IsNotNull();
                Condition.Requires(data, nameof(data)).IsNotNull();
                _thread = Thread.CurrentThread.ManagedThreadId;
                _outer = outer;
                _data = data;
            }

            public void Dispose()
            {
                Condition.Requires(Thread.CurrentThread.ManagedThreadId, nameof(Thread.CurrentThread.ManagedThreadId))
                    .IsEqualTo(_thread, "Override.Dispose() must be called from the same thread that has created it");
                if (_data.List != null) _outer._data.Remove(_data);
            }
        }

        // The caller must not mutate `p` or any of its subobjects after Add() returns.
        public IDisposable Add(Override p) => new Deleter(this, _data.AddLast(p));

        // It's OK to mutate the result. It doesn't alias anything.
        public Override AggregateCopy()
        {
            var res = new Override();
            foreach (Override data in _data) res.MergeFrom(data);
            return res;
        }
    }
}
