using Conditions;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using Tags = System.Collections.Generic.Dictionary<string, string>;
using Fields = System.Collections.Generic.Dictionary<string, InfluxDb.Field>;

namespace InfluxDb
{
    public class Facade
    {
        readonly Overrides _overrides = new Overrides();
        readonly ISink _sink;

        public Facade(ISink sink)
        {
            Condition.Requires(sink, "sink").IsNotNull();
            _sink = sink;
        }

        public void Push<TColumns>(string name, TColumns cols)
        {
            Push(name, cols, _overrides.UtcNow);
        }

        public void Push<TColumns>(string name, TColumns cols, DateTime t)
        {
            // It's OK to mutate `data`.
            TagsAndFields data = _overrides.TagsAndFields();
            Extract(cols, ref data.Tags, ref data.Fields);
            var p = new Point()
            {
                Key = new PointKey() { Name = name, Tags = data.Tags },
                Value = new PointValue() { Timestamp = t, Fields = data.Fields },
            };
            // The sink may mutate `p`. We must not mutate it.
            _sink.Push(p);
        }

        public void Push<TColumns>(TColumns cols)
        {
            Push(MeasurementExtractor<TColumns>.Name, cols);
        }

        public void Push<TColumns>(TColumns cols, DateTime t)
        {
            Push(MeasurementExtractor<TColumns>.Name, cols, t);
        }

        public IDisposable At(DateTime t)
        {
            return _overrides.Push(t, null);
        }

        public IDisposable With<TColumns>(DateTime t, TColumns cols)
        {
            var data = new TagsAndFields();
            Extract(cols, ref data.Tags, ref data.Fields);
            return _overrides.Push(t, data);
        }

        public IDisposable With<TColumns>(TColumns cols)
        {
            var data = new TagsAndFields();
            Extract(cols, ref data.Tags, ref data.Fields);
            return _overrides.Push(null, data);
        }

        static void Extract<TColumns>(TColumns cols, ref Tags tags, ref Fields fields)
        {
            tags = tags ?? new Tags();
            fields = fields ?? new Fields();
            var t = tags;
            var f = fields;
            MemberExtractor<TColumns>.Instance.Extract
            (
                cols,
                (key, val) => t[key] = val,  // the last value wins
                (key, val) => f[key] = val   // the last value wins
            );
        }
    }

    class TagsAndFields
    {
        public Tags Tags;
        public Fields Fields;

        public void MergeFrom(TagsAndFields other)
        {
            if (other == null) return;
            if (other.Tags != null)
            {
                Tags = Tags ?? new Tags();
                Tags.MergeFrom(other.Tags);
            }
            if (other.Fields != null)
            {
                Fields = Fields ?? new Fields();
                Fields.MergeFrom(other.Fields);
            }
        }
    }

    class Overrides
    {
        readonly object _monitor = new object();
        readonly LinkedList<DateTime> _time = new LinkedList<DateTime>();
        readonly LinkedList<TagsAndFields> _data = new LinkedList<TagsAndFields>();

        class Override : IDisposable
        {
            readonly Overrides _outer;
            readonly LinkedListNode<DateTime> _time;
            readonly LinkedListNode<TagsAndFields> _data;

            public Override(Overrides clock, LinkedListNode<DateTime> time, LinkedListNode<TagsAndFields> data)
            {
                Condition.Requires(clock, "clock").IsNotNull();
                _outer = clock;
                _time = time;
                _data = data;
            }

            public void Dispose()
            {
                lock (_outer._monitor)
                {
                    if (_time != null) _outer._time.Remove(_time);
                    if (_data != null) _outer._data.Remove(_data);
                }
            }
        }

        public IDisposable Push(DateTime? t, TagsAndFields data)
        {
            lock (_monitor)
            {
                return new Override
                (
                    this,
                    t.HasValue ? _time.AddLast(t.Value) : null,
                    data != null ? _data.AddLast(data) : null
                );
            }
        }

        public DateTime UtcNow
        {
            get
            {
                lock (_monitor)
                {
                    return _time.Any() ? _time.Last.Value : DateTime.UtcNow;
                }
            }
        }

        // It's OK to mutate the result.
        public TagsAndFields TagsAndFields()
        {
            lock (_monitor)
            {
                var res = new TagsAndFields() { Tags = new Tags(), Fields = new Fields() };
                foreach (TagsAndFields data in _data)
                {
                    res.MergeFrom(data);
                }
                return res;
            }
        }
    }
}
