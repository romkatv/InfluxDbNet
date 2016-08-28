using System.Collections.Generic;
using System.Linq;

namespace InfluxDb
{
    /// <summary>
    /// Comparer for sequences. Allows null elements and even null sequences.
    /// </summary>
    public class SequenceComparer<T> : IEqualityComparer<IEnumerable<T>>
    {
        public bool Equals(IEnumerable<T> x, IEnumerable<T> y)
        {
            if (x == null) return y == null;
            if (y == null) return x == null;
            return x.SequenceEqual(y);
        }

        public int GetHashCode(IEnumerable<T> seq)
        {
            if (seq == null) return 501107580;  // Random number.
            int res = -1623057131;  // Random number.
            foreach (T elem in seq)
            {
                res = Hash.HashWithSeed(res, elem);
            }
            return res;
        }
    }
}
