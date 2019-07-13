using Conditions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace InfluxDb {
  public enum Aggregation : byte {
    Last,
    Sum,
    Min,
    Max,
  }

  public struct Field {
    public enum FieldType : byte {
      None = 0,
      Long,
      Double,
      Bool,
      String
    }

    readonly Aggregation _aggregation;
    readonly FieldType _type;
    bool _bool;
    long _long;
    double _double;
    string _string;

    public static Field New(long val, Aggregation agg) => new Field(FieldType.Long, agg) { _long = val };
    public static Field New(double val, Aggregation agg) => new Field(FieldType.Double, agg) { _double = val };
    public static Field New(bool val, Aggregation agg) => new Field(FieldType.Bool, agg) { _bool = val };
    public static Field New(string val, Aggregation agg) => new Field(FieldType.String, agg) { _string = val };

    Field(FieldType type, Aggregation agg) {
      _aggregation = agg;
      _type = type;
      _bool = false;
      _long = 0;
      _double = 0;
      _string = null;
    }

    public bool HasValue => _type != FieldType.None;

    public void MergeWithOlder(in Field older) {
      if (_type != older._type) {
        throw new ArgumentException($"Can't merge fields of different types: {_type} vs {older._type}");
      }
      switch (_type) {
        case FieldType.Long:
          switch (_aggregation) {
            case Aggregation.Last:
              break;
            case Aggregation.Sum:
              _long += older._long;
              break;
            case Aggregation.Min:
              _long = Math.Min(_long, older._long);
              break;
            case Aggregation.Max:
              _long = Math.Max(_long, older._long);
              break;
          }
          break;
        case FieldType.Double:
          switch (_aggregation) {
            case Aggregation.Last:
              break;
            case Aggregation.Sum:
              _double += older._double;
              break;
            case Aggregation.Min:
              _double = Math.Min(_double, older._double);
              break;
            case Aggregation.Max:
              _double = Math.Max(_double, older._double);
              break;
          }
          break;
        case FieldType.Bool:
          switch (_aggregation) {
            case Aggregation.Last:
              break;
            case Aggregation.Sum:
              _bool = _bool || older._bool;
              break;
            case Aggregation.Min:
              _bool = _bool && older._bool;
              break;
            case Aggregation.Max:
              _bool = _bool || older._bool;
              break;
          }
          break;
        case FieldType.String:
          switch (_aggregation) {
            case Aggregation.Last:
              break;
            case Aggregation.Sum:
              if (_string != null || older._string != null) {
                _string = (older._string ?? "") + (_string ?? "");
              }
              break;
            case Aggregation.Min:
              if (string.CompareOrdinal(older._string, _string) < 0) _string = older._string;
              break;
            case Aggregation.Max:
              if (string.CompareOrdinal(older._string, _string) > 0) _string = older._string;
              break;
          }
          break;
      }
    }

    public bool SerializeTo(StringBuilder sb) {
      switch (_type) {
        case FieldType.Long:
          sb.Append(_long);
          sb.Append('i');
          return true;
        case FieldType.Double:
          sb.AppendFormat("{0:R}", _double);
          return !double.IsInfinity(_double) && !double.IsNaN(_double);
        case FieldType.Bool:
          sb.Append(_bool ? "true" : "false");
          return true;
        case FieldType.String:
          if (_string == null) {
            sb.Append("null");
            return false;
          } else {
            sb.Append('"');
            Strings.Escape(_string, '\\', Serializer.FieldSpecialChars, sb);
            sb.Append('"');
            return true;
          }
      }
      return false;
    }

    public override string ToString() {
      switch (_type) {
        case FieldType.Long:
          return $"long: {_long}";
        case FieldType.Double:
          return $"double: {_double}";
        case FieldType.Bool:
          return $"bool: {_bool}";
        case FieldType.String:
          return $"string: {Strings.Quote(_string)}";
      }
      return "<error>";
    }
  }

  static class NameTable {
    public static readonly InternTable Tags = new InternTable();
    public static readonly InternTable Fields = new InternTable();
  }

  public struct Indexed<T> {
    public Indexed(int index, in T value) {
      Index = index;
      Value = value;
    }

    public readonly int Index;
    public readonly T Value;
  }

  public class PartialPoint {
    static ThreadLocal<AutoBuf<int>> _buf = new ThreadLocal<AutoBuf<int>>(() => new AutoBuf<int>());

    public FastList<Indexed<string>> Tags;
    public FastList<Indexed<Field>> Fields;

    public void MergeFrom(PartialPoint p) {
      Tags.AddRange(p.Tags);
      Fields.AddRange(p.Fields);
    }

    public void Compact() {
      AutoBuf<int> buf = _buf.Value;
      Compact(ref Tags, buf);
      Compact(ref Fields, buf);
    }

    static void Compact<T>(ref FastList<Indexed<T>> list, AutoBuf<int> buf) {
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

  public struct PointKey : IEquatable<PointKey> {
    struct Stash {
      public bool Seen;
      public string Tag;
    }

    static ThreadLocal<AutoBuf<Stash>> _buf =
        new ThreadLocal<AutoBuf<Stash>>(() => new AutoBuf<Stash>());

    public string Name;
    public FastList<Indexed<string>> Tags;

    // Requires: There are no duplicate indices in Tags.
    public override int GetHashCode() {
      int res = Hash.HashAll(Name);
      for (int i = 0, e = Tags.Count; i != e; ++i) {
        Indexed<string> tag = Tags[i];
        // Using operator+ instead of Hash.Combine() to have hash independent of element order.
        res += Hash.HashWithSeed(tag.Index, tag.Value);
      }
      return res;
    }

    // Requires: There are no duplicate indices in Tags.
    public bool Equals(PointKey other) {
      if (other == null) return false;
      if (Name != other.Name) return false;
      if (Tags.Count != other.Tags.Count) return false;
      AutoBuf<Stash> buf = _buf.Value;
      for (int i = 0, e = Tags.Count; i != e; ++i) {
        buf[Tags[i].Index] = new Stash() { Tag = Tags[i].Value };
      }
      for (int i = 0, e = Tags.Count; i != e; ++i) {
        Indexed<string> tag = other.Tags[i];
        Stash stash = buf[tag.Index];
        if (stash.Seen) return false;
        stash.Seen = true;
        buf[tag.Index] = stash;
      }
      for (int i = 0, e = Tags.Count; i != e; ++i) {
        if (!buf[Tags[i].Index].Seen) return false;
      }
      return true;
    }

    public override bool Equals(object obj) => obj is PointKey k && Equals(k);
    public static bool operator ==(in PointKey x, in PointKey y) => x.Equals(y);
    public static bool operator !=(in PointKey x, in PointKey y) => !(x == y);

    public PointKey Clone() {
      var res = new PointKey() { Name = Name };
      res.Tags.AddRange(Tags);
      return res;
    }
  }

  public class PointValue {
    // Facade treats Timestamp equal to DateTime.MinValue as current time when Push() is called.
    public DateTime Timestamp;
    public FastList<Field> Fields;

    public PointValue() { }

    // Requires: There are no duplicate indices in fields.
    public PointValue(in DateTime t, in FastList<Indexed<Field>> fields) {
      Timestamp = t;
      for (int i = 0, e = fields.Count; i != e; ++i) {
        ref Indexed<Field> src = ref fields[i];
        while (Fields.Count <= src.Index) Fields.Add(new Field());
        Fields[src.Index] = src.Value;
      }
    }

    // Requires: There are no duplicate indices in fields.
    public void MergeWithNewer(in FastList<Indexed<Field>> newer, in DateTime t) {
      Condition.Requires(t, nameof(t)).IsGreaterOrEqual(Timestamp);
      for (int i = 0, e = newer.Count; i != e; ++i) {
        ref Indexed<Field> src = ref newer[i];
        while (Fields.Count <= src.Index) Fields.Add(new Field());
        ref Field dst = ref Fields[src.Index];
        if (dst.HasValue) {
          Field field = src.Value;
          field.MergeWithOlder(dst);
          dst = field;
        } else {
          dst = src.Value;
        }
      }
      Timestamp = t;
    }

    // Requires: There are no duplicate indices in fields.
    public void MergeWithOlder(in FastList<Indexed<Field>> older) {
      for (int i = 0, e = older.Count; i != e; ++i) {
        ref Indexed<Field> src = ref older[i];
        while (Fields.Count <= src.Index) Fields.Add(new Field());
        ref Field dst = ref Fields[src.Index];
        if (dst.HasValue) {
          dst.MergeWithOlder(src.Value);
        } else {
          dst = src.Value;
        }
      }
    }

    // Requires: There are no duplicate indices in fields.
    public void MergeWithOlder(PointValue older) {
      Condition.Requires(older.Timestamp, nameof(older.Timestamp)).IsLessOrEqual(Timestamp);
      for (int i = 0, e = Math.Min(Fields.Count, older.Fields.Count); i != e; ++i) {
        ref Field src = ref older.Fields[i];
        if (!src.HasValue) continue;
        ref Field dst = ref Fields[i];
        if (dst.HasValue) {
          dst.MergeWithOlder(src);
        } else {
          dst = src;
        }
      }
      if (Fields.Count < older.Fields.Count) {
        Fields.AddRange(older.Fields, Fields.Count, older.Fields.Count - Fields.Count);
      }
    }

    public PointValue Clone() {
      var res = new PointValue() { Timestamp = Timestamp };
      res.Fields.AddRange(Fields);
      return res;
    }
  }

  public class Point {
    public PointKey Key;
    public PointValue Value;
    public Point Clone() => new Point() { Key = Key, Value = Value?.Clone() };
  }

  public interface ISink {
    // Does not mutate `p`.
    void Push(string name, PartialPoint p, DateTime t);
  }

  public interface IBackend {
    // Timeout equal to TimeSpan.FromMilliseconds(-1) means infinity.
    Task Send(List<Point> points, TimeSpan timeout);
  }
}
