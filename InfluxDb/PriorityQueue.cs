using Conditions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InfluxDb
{
    // A queue where elements are sorted by key. If two elements have the same key,
    // the order of their insertion is preserved.
    class PriorityQueue<TKey, TValue>
    {
        readonly SortedDictionary<Tuple<TKey, long>, TValue> _data = new SortedDictionary<Tuple<TKey, long>, TValue>();
        long _index = 0;

        // Any elements in the queue?
        public bool Any()
        {
            return _data.Count > 0;
        }

        // Requires: queue is not empty.
        public KeyValuePair<TKey, TValue> Front()
        {
            Condition.Requires(_data, "_data").IsNotEmpty();
            var elem = _data.First();
            return new KeyValuePair<TKey, TValue>(elem.Key.Item1, elem.Value);
        }

        // Requires: queue is not empty.
        public KeyValuePair<TKey, TValue> Pop()
        {
            Condition.Requires(_data, "_data").IsNotEmpty();
            var elem = _data.First();
            var res = new KeyValuePair<TKey, TValue>(elem.Key.Item1, elem.Value);
            _data.Remove(elem.Key);
            return res;
        }

        public Func<bool> Push(TKey key, TValue value)
        {
            var id = Tuple.Create(key, _index++);
            _data.Add(id, value);
            return () => { return _data.Remove(id); };
        }
    }
}
