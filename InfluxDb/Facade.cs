using Conditions;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace InfluxDb
{
    public class Facade
    {
        readonly AdjustableClock _clock = new AdjustableClock();
        readonly ISink _sink;

        public Facade(ISink sink)
        {
            Condition.Requires(sink, "sink").IsNotNull();
            _sink = sink;
        }

        public void Push<TColumns>(string name, TColumns cols)
        {
            Push(name, cols, _clock.UtcNow);
        }

        public void Push<TColumns>(string name, TColumns cols, DateTime t)
        {
            var p = new Point()
            {
                Name = name,
                Timestamp = t,
                Tags = new SortedDictionary<string, string>(),
                Fields = new SortedDictionary<string, Field>(),
            };
            MemberExtractor<TColumns>.Instance.Extract
            (
                cols,
                (key, val) => p.Tags[key] = val,   // the last value wins
                (key, val) => p.Fields[key] = val  // the last value wins
            );
            _sink.Push(p);
        }

        public IDisposable At(DateTime t)
        {
            return _clock.At(t);
        }
    }

    interface IClock
    {
        DateTime UtcNow { get; }
    }

    class AdjustableClock : IClock
    {
        readonly object _monitor = new object();
        readonly LinkedList<DateTime> _overrides = new LinkedList<DateTime>();

        class Override : IDisposable
        {
            readonly AdjustableClock _clock;
            readonly LinkedListNode<DateTime> _node;

            public Override(AdjustableClock clock, LinkedListNode<DateTime> node)
            {
                Condition.Requires(clock, "clock").IsNotNull();
                Condition.Requires(node, "node").IsNotNull();
                _clock = clock;
                _node = node;
            }

            public void Dispose()
            {
                lock (_clock._monitor)
                {
                    _clock._overrides.Remove(_node);
                }
            }
        }

        public IDisposable At(DateTime t)
        {
            lock (_monitor)
            {
                return new Override(this, _overrides.AddLast(t));
            }
        }

        public DateTime UtcNow
        {
            get
            {
                lock (_monitor)
                {
                    return _overrides.Any() ? _overrides.Last.Value : DateTime.UtcNow;
                }
            }
        }
    }
}
