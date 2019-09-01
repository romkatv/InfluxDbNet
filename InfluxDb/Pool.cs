using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InfluxDb {
  public class Pool<T> {
    const int MaxSize = 32;

    T[] _data = new T[MaxSize];
    int _size = 0;

    public T Acquire() => _size == 0 ? default(T) : _data[--_size];
    
    public void Release(in T val) {
      if (_size < MaxSize) _data[_size++] = val;
    }
  }

  public class Pools {
    static System.Threading.ThreadLocal<Pools> _tls = new System.Threading.ThreadLocal<Pools>(() => new Pools());

    public Pool<LongField> LongField { get; } = new Pool<LongField>();
    public Pool<DoubleField> DoubleField { get; } = new Pool<DoubleField>();
    public Pool<BoolField> BoolField { get; } = new Pool<BoolField>();
    public Pool<StringField> StringField { get; } = new Pool<StringField>();

    public static Pools ThreadLocal => _tls.Value;
  }
}
