using Conditions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace InfluxDb {
  static class ListExtension {
    static ThreadLocal<AutoBuf<int>> _buf = new ThreadLocal<AutoBuf<int>>(() => new AutoBuf<int>());

    // Function `merge` is called as `merge(to_elem, from_elem)` where `from_elem` is never null.
    // It must not mutate its arguments.
    public static void MergeFrom<T>(this List<T> to, List<T> from, Func<T, T, T> merge) where T : class {
#if DEBUG
      Condition.Requires(from, nameof(from)).IsNotNull();
      Condition.Requires(to, nameof(to)).IsNotNull();
      Condition.Requires(merge, nameof(merge)).IsNotNull();
#endif
      for (int i = 0, e = Math.Min(from.Count, to.Count); i != e; ++i) {
        T src = from[i];
        if (src == null) continue;
        to[i] = merge.Invoke(to[i], src);
      }
      if (to.Count < from.Count) {
        to.Capacity = Math.Max(to.Capacity, from.Capacity);
        for (int i = to.Count; i != from.Count; ++i) to.Add(from[i] == null ? null : merge.Invoke(null, from[i]));
      }
    }

    public static void Compact<T>(this List<Indexed<T>> list) {
      list.Compact(_buf.Value);
    }

    static void Compact<T>(this List<Indexed<T>> list, AutoBuf<int> buf) {
      for (int i = 0, e = list.Count; i != e; ++i) {
        buf[list[i].Index] = i;
      }
      int j = 0;
      for (int i = 0, e = list.Count; i != e; ++i) {
        Indexed<T> x = list[i];
        if (buf[x.Index] == i) list[j++] = x;
      }
      list.RemoveRange(j, list.Count - j);
    }
  }
}
