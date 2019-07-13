using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InfluxDb {
  class AutoBuf<T> {
    T[] _data = new T[16];

    public T this[int idx] {
      get => _data[idx];
      set {
        if (_data.Length <= idx) {
          int capacity = _data.Length * 2;
          if (capacity <= idx) capacity = Bits.NextPow2(idx + 1);
          T[] bigger = new T[capacity];
          _data.CopyTo(bigger, 0);
          _data = bigger;
        }
        _data[idx] = value;
      }
    }
  }
}
