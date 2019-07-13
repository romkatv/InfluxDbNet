using Conditions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace InfluxDb {
  public struct FastList<T> {
    T[] _data;

    public FastList(int capacity) {
      _data = new T[capacity];
      Count = 0;
    }

    public int Count { get; internal set; }

    public ref T this[int index] {
      get {
#if DEBUG
        Condition.Requires(_data, nameof(_data)).IsNotNull();
        Condition.Requires(index, nameof(index)).IsGreaterOrEqual(0).IsLessThan(_data.Length);
#endif
        return ref _data[index];
      }
    }

    public void ResizeUninitialized(int size) {
      if (_data != null && size <= _data.Length) {
        Count = size;
        return;
      }
      Realloc(size);
      Count = size;
    }

    public void Add(in T val) {
      if (_data == null || Count == _data.Length) Realloc(Count + 1);
      _data[Count++] = val;
    }

    public void AddRange(in FastList<T> list, int start, int count) {
      if (count == 0) return;
      if (_data == null || _data.Length < Count + count) Realloc(Count + count);
      Array.Copy(list._data, start, _data, Count, count);
      Count += count;
    }

    public void AddRange(in FastList<T> list) {
      AddRange(list, 0, list.Count);
    }

    void Realloc(int capacity) {
      capacity = Math.Max(16, Bits.NextPow2(capacity));
      T[] bigger = new T[capacity];
      if (_data != null) Array.Copy(_data, bigger, Count);
      _data = bigger;
    }
  }

  static class FastListExtension {
    static ThreadLocal<AutoBuf<int>> _buf = new ThreadLocal<AutoBuf<int>>(() => new AutoBuf<int>());

    public static void Compact<T>(this FastList<Indexed<T>> list) {
      list.Compact(_buf.Value);
    }

    static void Compact<T>(this FastList<Indexed<T>> list, AutoBuf<int> buf) {
      for (int i = 0, e = list.Count; i != e; ++i) {
        buf[list[i].Index] = i;
      }
      int j = 0;
      for (int i = 0, e = list.Count; i != e; ++i) {
        Indexed<T> x = list[i];
        if (buf[x.Index] == i) list[j++] = x;
      }
      list.ResizeUninitialized(j);
    }
  }
}
