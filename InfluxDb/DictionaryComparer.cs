using Conditions;
using System.Collections.Generic;
using System.Linq;

namespace InfluxDb
{
    // Allows null elements and even null dictionaries.
    public class DictionaryComparer<TKey, TValue> : IEqualityComparer<Dictionary<TKey, TValue>>
    {
        readonly IEqualityComparer<TValue> _cmp;

        public DictionaryComparer()
        {
            _cmp = EqualityComparer<TValue>.Default;
        }

        public DictionaryComparer(IEqualityComparer<TValue> cmp)
        {
            Condition.Requires(cmp, "cmp").IsNotNull();
            _cmp = cmp;
        }

        public bool Equals(Dictionary<TKey, TValue> x, Dictionary<TKey, TValue> y)
        {
            if (x == null) return y == null;
            if (y == null) return false;
            if (x.Count != y.Count) return false;
            foreach (var kv in x)
            {
                TValue val;
                if (!y.TryGetValue(kv.Key, out val)) return false;
                if (!_cmp.Equals(kv.Value, val)) return false;
            }
            return true;
        }

        public int GetHashCode(Dictionary<TKey, TValue> dict)
        {
            if (dict == null) return 501107580;  // Random number.
            int res = -1623057131;  // Random number.
            foreach (var kv in dict)
            {
                unchecked
                {
                    res += Hash.Combine(dict.Comparer.GetHashCode(kv.Key), HashVal(kv.Value));
                }
            }
            return res;
        }

        int HashVal(TValue val)
        {
            if (val == null) return -1921273924;  // Random number.
            return _cmp.GetHashCode(val);
        }
    }
}
