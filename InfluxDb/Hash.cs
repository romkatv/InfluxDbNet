
namespace InfluxDb
{
    /// <summary>
    /// Helpers for hashing multiple values.
    /// </summary>
    public static class Hash
    {
        /// <summary>
        /// Combines two hashes into a single hash.
        /// </summary>
        public static int Combine(int seed, int val)
        {
            // Based on boost::hash_combine (http://www.boost.org/doc/libs/1_35_0/doc/html/hash/combine.html),
            // which is based on http://www.cs.rmit.edu.au/~jz/fulltext/jasist-tch.pdf.
            unchecked
            {
                uint a = (uint)seed;
                uint b = (uint)val;
                return (int)(a ^ (b + 0x9e3779b9 + (a << 6) + (a >> 2)));
            }
        }

        /// <summary>
        /// Returns a hash of the value combined with the seed. The value may be null.
        /// </summary>
        public static int HashWithSeed<T>(int seed, T val)
        {
            return Combine(seed, HashAll(val));
        }

        /// <summary>
        /// Returns a combined hash of all arguments. The arguments may be null.
        /// </summary>
        public static int HashAll<T1>(T1 v1)
        {
            if (v1 == null) return 1261422319;  // Random prime number.
            return v1.GetHashCode();
        }

        /// <summary>
        /// Returns a combined hash of all arguments. The arguments may be null.
        /// </summary>
        public static int HashAll<T1, T2>(T1 v1, T2 v2)
        {
            return HashWithSeed(HashAll(v1), v2);
        }

        /// <summary>
        /// Returns a combined hash of all arguments. The arguments may be null.
        /// </summary>
        public static int HashAll<T1, T2, T3>(T1 v1, T2 v2, T3 v3)
        {
            return HashWithSeed(HashAll(v1, v2), v3);
        }

        /// <summary>
        /// Returns a combined hash of all arguments. The arguments may be null.
        /// </summary>
        public static int HashAll<T1, T2, T3, T4>(T1 v1, T2 v2, T3 v3, T4 v4)
        {
            return HashWithSeed(HashAll(v1, v2, v3), v4);
        }

        /// <summary>
        /// Returns a combined hash of all arguments. The arguments may be null.
        /// </summary>
        public static int HashAll<T1, T2, T3, T4, T5>(T1 v1, T2 v2, T3 v3, T4 v4, T5 v5)
        {
            return HashWithSeed(HashAll(v1, v2, v3, v4), v5);
        }
    }
}
