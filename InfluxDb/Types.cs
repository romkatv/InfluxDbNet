using Conditions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InfluxDb {
  public enum Aggregation {
    Last,
    Sum,
    Min,
    Max,
  }

  public abstract class Field {
    protected Aggregation _aggregation;

    public static Field New(long val, Aggregation aggregation = Aggregation.Last) {
      return new LongField() { Val = val, _aggregation = aggregation };
    }
    public static Field New(double val, Aggregation aggregation = Aggregation.Last) {
      return new DoubleField() { Val = val, _aggregation = aggregation };
    }
    public static Field New(bool val, Aggregation aggregation = Aggregation.Last) {
      return new BoolField() { Val = val, _aggregation = aggregation };
    }
    public static Field New(string val, Aggregation aggregation = Aggregation.Last) {
      return new StringField() { Val = val, _aggregation = aggregation };
    }

    public Field Clone() => (Field)MemberwiseClone();

    public void MergeWithOlder(Field older) {
      Condition.Requires(older, nameof(older)).IsNotNull();
      if (GetType() != older.GetType()) {
        throw new ArgumentException(string.Format(
                "Can't merge fields of different types: {0} vs {1}",
                GetType().Name, older.GetType().Name));
      }
      if (_aggregation != older._aggregation) {
        throw new ArgumentException(string.Format(
            "Aggregation function must be the same for two values to be mergeable: {0} vs {1}",
            _aggregation, older._aggregation));
      }
      MergeWithOlderImpl(older);
    }

    public abstract bool SerializeTo(StringBuilder sb);

    public abstract override string ToString();

    protected abstract void MergeWithOlderImpl(Field older);

    sealed class LongField : Field {
      public long Val;

      public override bool SerializeTo(StringBuilder sb) {
        sb.Append(Val);
        sb.Append('i');
        return true;
      }

      public override string ToString() => $"long: {Val}";

      protected override void MergeWithOlderImpl(Field older) {
        switch (_aggregation) {
          case Aggregation.Last:
            break;
          case Aggregation.Sum:
            Val += ((LongField)older).Val;
            break;
          case Aggregation.Min:
            Val = Math.Min(Val, ((LongField)older).Val);
            break;
          case Aggregation.Max:
            Val = Math.Max(Val, ((LongField)older).Val);
            break;
        }
      }
    }

    sealed class DoubleField : Field {
      public double Val;

      public override bool SerializeTo(StringBuilder sb) {
        sb.AppendFormat("{0:R}", Val);
        return !double.IsInfinity(Val) && !double.IsNaN(Val);
      }

      public override string ToString() => $"double: {Val}";

      protected override void MergeWithOlderImpl(Field older) {
        switch (_aggregation) {
          case Aggregation.Last:
            break;
          case Aggregation.Sum:
            Val += ((DoubleField)older).Val;
            break;
          case Aggregation.Min:
            Val = Math.Min(Val, ((DoubleField)older).Val);
            break;
          case Aggregation.Max:
            Val = Math.Max(Val, ((DoubleField)older).Val);
            break;
        }
      }
    }

    sealed class BoolField : Field {
      public bool Val;

      public override bool SerializeTo(StringBuilder sb) {
        sb.Append(Val ? "true" : "false");
        return true;
      }

      public override string ToString() => $"bool: {Val}";

      protected override void MergeWithOlderImpl(Field older) {
        switch (_aggregation) {
          case Aggregation.Last:
            break;
          case Aggregation.Sum:
            Val = Val || ((BoolField)older).Val;
            break;
          case Aggregation.Min:
            Val = Val && ((BoolField)older).Val;
            break;
          case Aggregation.Max:
            Val = Val || ((BoolField)older).Val;
            break;
        }
      }
    }

    sealed class StringField : Field {
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

      public override string ToString() => $"string: {Val}";

      protected override void MergeWithOlderImpl(Field older) {
        string s = ((StringField)older).Val;
        switch (_aggregation) {
          case Aggregation.Last:
            break;
          case Aggregation.Sum:
            if (Val != null || s != null) Val = (s ?? "") + (Val ?? "");
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
  }

  static class NameTable {
    public static readonly InternTable Tags = new InternTable();
    public static readonly InternTable Fields = new InternTable();
  }

  public class PointKey : IEquatable<PointKey> {
    public string Name { get; set; }
    public List<string> Tags { get; set; }

    public override int GetHashCode() {
      int res = Hash.HashAll(Name);
      if (Tags == null) return Hash.HashWithSeed(res, Tags);
      res = Hash.Combine(res, Tags.Count);
      foreach (string tag in Tags) res = Hash.HashWithSeed(res, tag);
      return res;
    }

    public bool Equals(PointKey other) {
      if (other == null) return false;
      if (Name != other.Name) return false;
      if (Tags == null) return other.Tags == null;
      if (other.Tags == null) return false;
      if (Tags.Count != other.Tags.Count) return false;
      for (int i = 0; i != Tags.Count; ++i) {
        if (Tags[i] != other.Tags[i]) return false;
      }
      return true;
    }

    public override bool Equals(object obj) => Equals(obj as PointKey);
    public static bool operator ==(PointKey x, PointKey y) => x is null ? y is null : x.Equals(y);
    public static bool operator !=(PointKey x, PointKey y) => !(x == y);
  }

  public class PointValue {
    // Facade treats Timestamp equal to DateTime.MinValue as current time when Push() is called.
    public DateTime Timestamp { get; set; }
    public List<Field> Fields { get; set; }

    public PointValue Clone() {
      var res = new PointValue() { Timestamp = Timestamp };
      if (Fields != null) {
        res.Fields = new List<Field>(Fields.Capacity);
        foreach (Field field in Fields) res.Fields.Add(field?.Clone());
      }
      return res;
    }

    // Mutates `this` but not `older`. After the call, `this` may contain references
    // to the objects in `older`. It's best to never use `older` afterwards.
    public void MergeWithOlder(PointValue older) {
      Condition.Requires(older.Timestamp, nameof(older.Timestamp)).IsLessOrEqual(Timestamp);
      if (older.Fields == null) return;
      if (Fields == null) {
        Fields = older.Fields;
        return;
      }
      Fields.MergeFrom(older.Fields, (Field to, Field from) => {
        if (to == null) return from;
        to.MergeWithOlder(from);
        return to;
      });
    }
  }

  public class Point {
    public PointKey Key { get; set; }
    public PointValue Value { get; set; }
  }

  public class PartialPoint {
    // Can be null.
    public List<string> Tags { get; set; }
    // Can be null.
    public List<Field> Fields { get; set; }

    public void MergeFrom(PartialPoint other) {
      if (other == null) return;
      if (other.Tags != null) {
        if (Tags == null) {
          Tags = new List<string>(other.Tags);
        } else {
          Tags.MergeFrom(other.Tags, (string to, string from) => from);
        }
      }
      if (other.Fields != null) {
        if (Fields == null) Fields = new List<Field>(other.Fields.Capacity);
        Fields.MergeFrom(other.Fields, (Field to, Field from) => from.Clone());
      }
    }
  }

  public interface ISink {
    // The caller must not mutate `p`. ISink may mutate it.
    void Push(Point p);
  }

  public interface IBackend {
    // Timeout equal to TimeSpan.FromMilliseconds(-1) means infinity.
    Task Send(List<Point> points, TimeSpan timeout);
  }
}
