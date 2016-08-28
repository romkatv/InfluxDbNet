
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
        public static int HashAll<T>(T a)
        {
            if (a == null) return 73388878;  // Random number.
            return a.GetHashCode();
        }

        /// <summary>
        /// Returns a combined hash of all arguments. The arguments may be null.
        /// </summary>
        public static int HashAll<T1, T2>(T1 a, T2 b)
        {
            return HashWithSeed(HashAll(a), b);
        }

        /// <summary>
        /// Returns a combined hash of all arguments. The arguments may be null.
        /// </summary>
        public static int HashAll<T1, T2, T3>(T1 a, T2 b, T3 c)
        {
            return HashWithSeed(HashAll(a, b), c);
        }
    }
}
