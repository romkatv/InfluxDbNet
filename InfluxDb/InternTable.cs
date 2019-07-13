using Conditions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace InfluxDb {
  class InternTable {
    readonly object _monitor = new object();
    string[] _byIdx = new string[16];
    Dictionary<string, int> _byStr = new Dictionary<string, int>();

    public string[] Array { get => Volatile.Read(ref _byIdx); }

    public string Get(int idx) => Array[idx];

    public int Intern(string s) {
      Condition.Requires(s, nameof(s)).IsNotNull();
      lock (_monitor) {
        if (_byStr.TryGetValue(s, out int idx)) return idx;
        idx = _byStr.Count;
        if (_byIdx.Length == idx) {
          string[] byIdx = new string[2 * _byIdx.Length];
          _byIdx.CopyTo(byIdx, 0);
          Volatile.Write(ref _byIdx, byIdx);
        }
        _byIdx[idx] = s;
        _byStr.Add(s, idx);
        return idx;
      }
    }
  }
}
