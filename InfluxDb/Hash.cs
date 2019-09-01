
namespace InfluxDb {
  public static class Hash {
    public static ulong Mix(ulong val) => Combine(0x4b2fc6ef, val);
    public static ulong Mix(int val) => Mix((ulong)val);
    public static ulong Combine(ulong seed, int val) => Combine(seed, (ulong)val);
    public static void Mix(ref ulong val) => val = Mix(val);
    public static void Combine(ref ulong seed, int val) => seed = Combine(seed, val);
    public static void Combine(ref ulong seed, ulong val) => seed = Combine(seed, val);

    public static ulong Combine(ulong seed, ulong val) {
      unchecked {
        ulong x = seed + val;
        x *= 0xcc9e2d51;
        return (x ^ (x >> 32));
      }
    }
  }
}
