using Conditions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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

  public abstract class Field {
    protected Aggregation _aggregation;

    internal static Field New(long val, Aggregation aggregation, Pools p) {
      var res = p.LongField.Acquire() ?? new LongField() { _aggregation = aggregation };
      res.Val = val;
      return res;
    }
    internal static Field New(double val, Aggregation aggregation, Pools p) {
      var res = p.DoubleField.Acquire() ?? new DoubleField() { _aggregation = aggregation };
      res.Val = val;
      return res;
    }
    internal static Field New(bool val, Aggregation aggregation, Pools p) {
      var res = p.BoolField.Acquire() ?? new BoolField() { _aggregation = aggregation };
      res.Val = val;
      return res;
    }
    internal static Field New(string val, Aggregation aggregation, Pools p) {
      var res = p.StringField.Acquire() ?? new StringField() { _aggregation = aggregation };
      res.Val = val;
      return res;
    }

    public void MergeWithNewer(Field newer) {
      Debug.Assert(newer != null);
      if (GetType() != newer.GetType()) {
        throw new ArgumentException(string.Format(
                "Can't merge fields of different types: {0} vs {1}",
                GetType().Name, newer.GetType().Name));
      }
      if (_aggregation != newer._aggregation) {
        throw new ArgumentException(string.Format(
            "Aggregation function must be the same for two values to be mergeable: {0} vs {1}",
            _aggregation, newer._aggregation));
      }
      MergeWithNewerImpl(newer);
    }

    public abstract bool SerializeTo(StringBuilder sb);

    public abstract override string ToString();

    public abstract Field Clone(Pools p);

    internal abstract void Release(Pools p);

    protected abstract void MergeWithNewerImpl(Field older);
  }

  public sealed class LongField : Field {
    public long Val;

    public override bool SerializeTo(StringBuilder sb) {
      sb.Append(Val);
      sb.Append('i');
      return true;
    }

    public override string ToString() => $"long: {Val}";

    public override Field Clone(Pools p) => Field.New(Val, _aggregation, p);

    internal override void Release(Pools p) => p.LongField.Release(this);

    protected override void MergeWithNewerImpl(Field newer) {
      switch (_aggregation) {
        case Aggregation.Last:
          Val = ((LongField)newer).Val;
          break;
        case Aggregation.Sum:
          Val += ((LongField)newer).Val;
          break;
        case Aggregation.Min:
          Val = Math.Min(Val, ((LongField)newer).Val);
          break;
        case Aggregation.Max:
          Val = Math.Max(Val, ((LongField)newer).Val);
          break;
      }
    }
  }

  public sealed class DoubleField : Field {
    public double Val;

    public override bool SerializeTo(StringBuilder sb) {
      sb.AppendFormat("{0:R}", Val);
      return !double.IsInfinity(Val) && !double.IsNaN(Val);
    }

    public override string ToString() => $"double: {Val}";

    public override Field Clone(Pools p) => Field.New(Val, _aggregation, p);

    internal override void Release(Pools p) => p.DoubleField.Release(this);

    protected override void MergeWithNewerImpl(Field newer) {
      switch (_aggregation) {
        case Aggregation.Last:
          Val = ((DoubleField)newer).Val;
          break;
        case Aggregation.Sum:
          Val += ((DoubleField)newer).Val;
          break;
        case Aggregation.Min:
          Val = Math.Min(Val, ((DoubleField)newer).Val);
          break;
        case Aggregation.Max:
          Val = Math.Max(Val, ((DoubleField)newer).Val);
          break;
      }
    }
  }

  public sealed class BoolField : Field {
    public bool Val;

    public override bool SerializeTo(StringBuilder sb) {
      sb.Append(Val ? "true" : "false");
      return true;
    }

    public override string ToString() => $"bool: {Val}";

    public override Field Clone(Pools p) => Field.New(Val, _aggregation, p);

    internal override void Release(Pools p) => p.BoolField.Release(this);

    protected override void MergeWithNewerImpl(Field newer) {
      switch (_aggregation) {
        case Aggregation.Last:
          Val = ((BoolField)newer).Val;
          break;
        case Aggregation.Sum:
          Val = Val || ((BoolField)newer).Val;
          break;
        case Aggregation.Min:
          Val = Val && ((BoolField)newer).Val;
          break;
        case Aggregation.Max:
          Val = Val || ((BoolField)newer).Val;
          break;
      }
    }
  }

  public sealed class StringField : Field {
    public string Val;

    public override bool SerializeTo(StringBuilder sb) {
      if (Val == null) {
        sb.Append("null");
        return false;
      } else {
        sb.Append('"');
        Strings.Escape(Val, '\\', Serializer.FieldSpecialChars, sb);
        sb.Append('"');
        return true;
      }
    }

    public override string ToString() => $"string: {Strings.Quote(Val)}";

    public override Field Clone(Pools p) => Field.New(Val, _aggregation, p);

    internal override void Release(Pools p) => p.StringField.Release(this);

    protected override void MergeWithNewerImpl(Field newer) {
      string s = ((StringField)newer).Val;
      switch (_aggregation) {
        case Aggregation.Last:
          Val = s;
          break;
        case Aggregation.Sum:
          if (Val == null) {
            Val = s;
          } else if (s != null) {
            Val += s;
          }
          break;
        case Aggregation.Min:
          if (string.CompareOrdinal(s, Val) < 0) Val = s;
          break;
        case Aggregation.Max:
          if (string.CompareOrdinal(s, Val) > 0) Val = s;
          break;
      }
    }
  }

  static class NameTable {
    public static readonly InternTable Tags = new InternTable();
    public static readonly InternTable Fields = new InternTable();
  }

  public struct Indexed<T> {
    public int Index;
    public T Value;
  }

  public struct Versioned<T> {
    public ulong Version;
    public T Value;
  }

  public class PartialPoint : IntrusiveListNode<PartialPoint> {
    public DateTime? Timestamp;
    public FastList<Indexed<string>> Tags;
    public FastList<Indexed<Field>> Fields;

    public void CopyFrom(Pools pools, PartialPoint other) {
      Timestamp = other.Timestamp;
      Tags.ResizeUninitialized(0);
      Fields.ResizeUninitialized(0);
      Tags.AddRange(other.Tags);
      Fields.AddRange(other.Fields);
      for (int i = 0, e = Fields.Count; i != e; ++i) {
        ref Field field = ref Fields[i].Value;
        if (field != null) field = field.Clone(pools);
      }
    }
  }

  public class ShardedPoint {
    public PartialPoint Global;
    public IntrusiveListNode<PartialPoint>.List Local = new IntrusiveListNode<PartialPoint>.List();
    public PartialPoint Final = new PartialPoint();
    public Pools Pools = new Pools();
  }

  public struct PointKey : IEquatable<PointKey> {
    class Dense {
      public ulong Mask;
      public ulong Hash;
      public string[] Tags;
    }

    public string Name;

    // Either ShardedPoint or Dense.
    public object Tags;

    public IEnumerable<KeyValuePair<string, string>> GetTags() {
      string[] names = NameTable.Tags.Array;
      if (Tags is Dense d) {
        for (int i = 0; i != d.Tags.Length; ++i) {
          string v = d.Tags[i];
          if (v != null) {
            yield return new KeyValuePair<string, string>(names[i], v);
          }
        }
      } else {
        var s = (ShardedPoint)Tags;
        Debug.Assert(s != null);
        ulong mask = 0;
        IEnumerable<KeyValuePair<string, string>> TagsFromPoint(PartialPoint p) {
          for (int i = p.Tags.Count - 1; i >= 0; --i) {
            Indexed<string> tag = p.Tags[i];
            if (Bits.TrySet(ref mask, tag.Index)) {
              yield return new KeyValuePair<string, string>(names[tag.Index], tag.Value);
            }
          }
        }
        foreach (var kv in TagsFromPoint(s.Final)) yield return kv;
        for (PartialPoint node = s.Local.Last; node != null; node = node.Prev) {
          foreach (var kv in TagsFromPoint(node)) yield return kv;
        }
        foreach (var kv in TagsFromPoint(s.Global)) yield return kv;
      }
    }

    public override int GetHashCode() {
      Debug.Assert(Name != null);
      Debug.Assert(Tags != null);
      if (Tags is Dense d) {
        unchecked { return (int)d.Hash; }
      }
      var p = (ShardedPoint)Tags;
      Debug.Assert(p != null);
      ulong hash = Hash.Mix(Name.GetHashCode());
      ulong mask = 0;
      HashPoint(ref mask, ref hash, p.Final);
      for (PartialPoint node = p.Local.Last; node != null; node = node.Prev) {
        HashPoint(ref mask, ref hash, node);
      }
      HashPoint(ref mask, ref hash, p.Global);
      Hash.Combine(ref hash, mask);
      unchecked { return (int)hash; }
    }

    public bool Equals(PointKey other) {
      if (other == null) return false;
      if (Name != other.Name) return false;
      {
        if (Tags is ShardedPoint p) return Eq(p, (Dense)other.Tags);
      }
      {
        return other.Tags is ShardedPoint p ? Eq(p, (Dense)Tags) : Eq((Dense)Tags, (Dense)other.Tags);
      }
    }

    public override bool Equals(object obj) => obj is PointKey k && Equals(k);
    public static bool operator ==(in PointKey x, in PointKey y) => x.Equals(y);
    public static bool operator !=(in PointKey x, in PointKey y) => !(x == y);

    public void Compact() {
      var p = (ShardedPoint)Tags;
      Debug.Assert(p != null);
      var d = new Dense() {
        Mask = 0,
        Hash = Hash.Mix(Name.GetHashCode()),
        Tags = new string[NameTable.Tags.Array.Length],
      };
      CompactPoint(d, p.Final);
      for (PartialPoint node = p.Local.Last; node != null; node = node.Prev) {
        CompactPoint(d, node);
      }
      CompactPoint(d, p.Global);
      Hash.Combine(ref d.Hash, d.Mask);
      Tags = d;
    }

    static void HashPoint(ref ulong mask, ref ulong hash, PartialPoint p) {
      for (int i = p.Tags.Count - 1; i >= 0; --i) {
        ref Indexed<string> tag = ref p.Tags[i];
        if (Bits.TrySet(ref mask, tag.Index)) Hash.Combine(ref hash, tag.Value.GetHashCode());
      }
    }

    static void CompactPoint(Dense d, PartialPoint p) {
      for (int i = p.Tags.Count - 1; i >= 0; --i) {
        ref Indexed<string> tag = ref p.Tags[i];
        ref string slot = ref d.Tags[tag.Index];
        if (slot == null) {
          slot = tag.Value;
          Hash.Combine(ref d.Hash, tag.Value.GetHashCode());
        }
      }
    }

    static bool Eq(ShardedPoint p, Dense d) {
      Debug.Assert(p != null);
      Debug.Assert(d != null);
      ulong mask = 0;
      if (!EqPoint(ref mask, p.Final, d.Tags)) return false;
      for (PartialPoint node = p.Local.Last; node != null; node = node.Prev) {
        if (!EqPoint(ref mask, node, d.Tags)) return false;
      }
      if (!EqPoint(ref mask, p.Global, d.Tags)) return false;
      return mask == d.Mask;
    }

    static bool Eq(Dense x, Dense y) {
      Debug.Assert(x != null);
      Debug.Assert(y != null);
      if (x.Mask != y.Mask) return false;
      for (int i = 0, e = Math.Min(x.Tags.Length, y.Tags.Length); i != e; ++i) {
        if (x.Tags[i] != y.Tags[i]) return false;
      }
      return true;
    }

    static bool EqPoint(ref ulong mask, PartialPoint p, string[] tags) {
      for (int i = p.Tags.Count - 1; i >= 0; --i) {
        ref Indexed<string> tag = ref p.Tags[i];
        if (Bits.TrySet(ref mask, tag.Index) && tag.Value != tags[tag.Index]) return false;
      }
      return true;
    }
  }

  public class PointValue {
    // Facade treats Timestamp equal to DateTime.MinValue as current time when Push() is called.
    public DateTime Timestamp;
    public FastList<Versioned<Field>> Fields;

    public IEnumerable<KeyValuePair<string, Field>> GetFields() {
      string[] names = NameTable.Fields.Array;
      for (int i = 0; i != Fields.Count; ++i) {
        Field field = Fields[i].Value;
        if (field != null) {
          yield return new KeyValuePair<string, Field>(names[i], field);
        }
      }
    }

    public void MergeWith(ShardedPoint p, DateTime t, ulong version) {
      bool newer = t >= Timestamp;
      if (newer) Timestamp = t;
      Fields.ResizeUninitialized(NameTable.Fields.Array.Length);
      MergePoint(p.Final, version, newer, p.Pools, from_pool: true);
      for (PartialPoint node = p.Local.Last; node != null; node = node.Prev) {
        MergePoint(node, version, newer, p.Pools, from_pool: false);
      }
      MergePoint(p.Global, version, newer, p.Pools, from_pool: true);
    }

    public void MergeWithNewer(PointValue p, Pools pools) {
      Condition.Requires(p.Timestamp, nameof(p.Timestamp)).IsGreaterOrEqual(Timestamp);
      Timestamp = p.Timestamp;
      if (Fields.Count < p.Fields.Count) Fields.ResizeUninitialized(p.Fields.Count);
      for (int i = 0, e = p.Fields.Count; i != e; ++i) {
        Field other = p.Fields[i].Value;
        if (other == null) continue;
        ref Field my = ref Fields[i].Value;
        MergeField(pools, ref Fields[i].Value, other, newer: true, from_pool: true);
      }
    }

    public PointValue Clone(Pools pools) {
      var res = new PointValue() { Timestamp = Timestamp };
      res.Fields.AddRange(Fields);
      for (int i = 0, e = Fields.Count; i != e; ++i) {
        ref Field field = ref res.Fields[i].Value;
        if (field != null) field = field.Clone(pools);
      }
      return res;
    }

    void MergePoint(PartialPoint p, ulong version, bool newer, Pools pools, bool from_pool) {
      for (int i = p.Fields.Count - 1; i >= 0; --i) {
        ref Indexed<Field> other = ref p.Fields[i];
        ref Versioned<Field> my = ref Fields[other.Index];
        if (my.Version == version) {
          if (pools != null) other.Value.Release(pools);
          continue;
        }
        my.Version = version;
        MergeField(pools, ref my.Value, other.Value, newer, from_pool);
      }
    }

    void MergeField(Pools pools, ref Field my, Field other, bool newer, bool from_pool) {
      if (my == null) {
        my = from_pool ? other : other.Clone(pools);
        return;
      }
      if (newer) {
        my.MergeWithNewer(other);
        if (from_pool) other.Release(pools);
      } else {
        if (!from_pool) other = other.Clone(pools);
        other.MergeWithNewer(my);
        my.Release(pools);
        my = other;
      }
    }
  }

  public class Point {
    public PointKey Key;
    public PointValue Value;
  }

  public interface ISink {
    void Push(string name, ShardedPoint p);
  }

  public interface IBackend {
    // Timeout equal to TimeSpan.FromMilliseconds(-1) means infinity.
    Task Send(List<Point> points, TimeSpan timeout);
  }
}
