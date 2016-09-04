using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InfluxDb
{
    static class DictionaryExtension
    {
        // The left argument is mutated.
        // The right argument wins in case of key conflicts.
        public static void MergeFrom<TKey, TValue>(
            this IDictionary<TKey, TValue> x, IDictionary<TKey, TValue> y)
        {
            foreach (var kv in y)
            {
                x[kv.Key] = kv.Value;
            }
        }
    }
}
