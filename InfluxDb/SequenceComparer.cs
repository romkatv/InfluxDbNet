using Conditions;
using System.Collections.Generic;
using System.Linq;

namespace InfluxDb
{
    /// <summary>
    /// Comparer for sequences. Allows null elements and even null sequences.
    /// </summary>
    public class SequenceComparer<T> : IEqualityComparer<IEnumerable<T>>
    {
        readonly IEqualityComparer<T> _cmp;

        public SequenceComparer()
        {
            _cmp = EqualityComparer<T>.Default;
        }

        public SequenceComparer(IEqualityComparer<T> cmp)
        {
            Condition.Requires(cmp, "cmp").IsNotNull();
            _cmp = cmp;
        }
         
        public bool Equals(IEnumerable<T> x, IEnumerable<T> y)
        {
            if (x == null) return y == null;
            if (y == null) return x == null;
            return x.SequenceEqual(y, _cmp);
        }

        public int GetHashCode(IEnumerable<T> seq)
        {
            if (seq == null) return 501107580;  // Random number.
            int res = -1623057131;  // Random number.
            foreach (T elem in seq)
            {
                res = Hash.HashWithSeed(res, HashElem(elem));
            }
            return res;
        }

        int HashElem(T elem)
        {
            if (elem == null) return -1921273924;  // Random number.
            return _cmp.GetHashCode(elem);
        }
    }
}
