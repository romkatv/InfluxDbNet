using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InfluxDb {
  static class Bits {
    public static uint NextPow2(uint x) {
      x--;
      x |= x >> 1;
      x |= x >> 2;
      x |= x >> 4;
      x |= x >> 8;
      x |= x >> 16;
      return x + 1;
    }

    public static int NextPow2(int x) => (int)NextPow2((uint)x);
  }
}
