using Conditions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InfluxDb
{
    public enum Aggregation
    {
        Last,
        Sum,
        Min,
        Max,
    }

    // Essentially Variant<long, double, bool, string>.
    public abstract class Field : IEquatable<Field>
    {
        protected Aggregation _aggregation;

        public static Field New(long val, Aggregation aggregation = Aggregation.Last)
        {
            return new C0() { Val = val, _aggregation = aggregation };
        }
        public static Field New(double val, Aggregation aggregation = Aggregation.Last)
        {
            return new C1() { Val = val, _aggregation = aggregation };
        }
        public static Field New(bool val, Aggregation aggregation = Aggregation.Last)
        {
            return new C2() { Val = val, _aggregation = aggregation };
        }
        public static Field New(string val, Aggregation aggregation = Aggregation.Last)
        {
            return new C3() { Val = val, _aggregation = aggregation };
        }

        /// <summary>
        /// Calls exactly one of the passed functions passing it the currently held value.
        /// </summary>
        public abstract R Visit<R>(Func<long, R> v0, Func<double, R> v1, Func<bool, R> v2, Func<string, R> v3);

        /// <summary>
        /// Zero-based index of the active value.
        /// </summary>
        public int Index()
        {
            return Visit((v) => 0, (v) => 1, (v) => 2, (v) => 3);
        }

        /// <summary>
        /// Type of the active value.
        /// </summary>
        public Type Type()
        {
            return Visit((v) => typeof(long), (v) => typeof(double), (v) => typeof(bool), (v) => typeof(string));
        }

        /// <summary>
        /// Currently active value as Object.
        /// </summary>
        public object Value()
        {
            return Visit<object>((long v) => v, (double v) => v, (bool v) => v, (string v) => v);
        }

        public Field Clone()
        {
            return Visit((long v) => New(v, _aggregation), (double v) => New(v, _aggregation),
                         (bool v) => New(v, _aggregation), (string v) => New(v, _aggregation));
        }

        public void MergeWithOlder(Field older)
        {
            Condition.Requires(older, "older").IsNotNull();
            if (GetType() != older.GetType())
            {
                throw new ArgumentException(string.Format(
                        "Can't merge fields of different types: {0} vs {1}",
                        Type().Name, older.Type().Name));
            }
            if (_aggregation != older._aggregation)
            {
                throw new ArgumentException(string.Format(
                    "Aggregation function must be the same for two values to be mergeable: {0} vs {1}",
                    _aggregation, older._aggregation));
            }
            MergeWithOlderImpl(older);
        }

        public override string ToString()
        {
            return String.Format("{0}: {1}", Type().FullName, Value());
        }

        public bool Equals(Field other)
        {
            if (other == null) return false;
            if (Index() != other.Index()) return false;
            return Object.Equals(Value(), other.Value());
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as Field);
        }

        public override int GetHashCode()
        {
            return Hash.HashWithSeed(Index(), Value());
        }

        protected abstract void MergeWithOlderImpl(Field older);

        sealed class C0 : Field
        {
            public long Val;

            public override R Visit<R>(Func<long, R> v0, Func<double, R> v1, Func<bool, R> v2, Func<string, R> v3)
            {
                return v0.Invoke(Val);
            }

            protected override void MergeWithOlderImpl(Field older)
            {
                switch (_aggregation)
                {
                    case Aggregation.Last:
                        break;
                    case Aggregation.Sum:
                        Val += ((C0)older).Val;
                        break;
                    case Aggregation.Min:
                        Val = Math.Min(Val, ((C0)older).Val);
                        break;
                    case Aggregation.Max:
                        Val = Math.Max(Val, ((C0)older).Val);
                        break;
                }
            }
        }
        sealed class C1 : Field
        {
            public double Val;

            public override R Visit<R>(Func<long, R> v0, Func<double, R> v1, Func<bool, R> v2, Func<string, R> v3)
            {
                return v1.Invoke(Val);
            }

            protected override void MergeWithOlderImpl(Field older)
            {
                switch (_aggregation)
                {
                    case Aggregation.Last:
                        break;
                    case Aggregation.Sum:
                        Val += ((C1)older).Val;
                        break;
                    case Aggregation.Min:
                        Val = Math.Min(Val, ((C1)older).Val);
                        break;
                    case Aggregation.Max:
                        Val = Math.Max(Val, ((C1)older).Val);
                        break;
                }
            }
        }
        sealed class C2 : Field
        {
            public bool Val;

            public override R Visit<R>(Func<long, R> v0, Func<double, R> v1, Func<bool, R> v2, Func<string, R> v3)
            {
                return v2.Invoke(Val);
            }

            protected override void MergeWithOlderImpl(Field older)
            {
                switch (_aggregation)
                {
                    case Aggregation.Last:
                        break;
                    case Aggregation.Sum:
                        Val = Val || ((C2)older).Val;
                        break;
                    case Aggregation.Min:
                        Val = Val && ((C2)older).Val;
                        break;
                    case Aggregation.Max:
                        Val = Val || ((C2)older).Val;
                        break;
                }
            }
        }
        sealed class C3 : Field
        {
            public string Val;

            public override R Visit<R>(Func<long, R> v0, Func<double, R> v1, Func<bool, R> v2, Func<string, R> v3)
            {
                return v3.Invoke(Val);
            }

            protected override void MergeWithOlderImpl(Field older)
            {
                string s = ((C3)older).Val;
                switch (_aggregation)
                {
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

    public class PointKey
    {
        public string Name { get; set; }
        public Dictionary<string, string> Tags { get; set; }
    }

    public class PointValue
    {
        public DateTime Timestamp { get; set; }
        public Dictionary<string, Field> Fields { get; set; }

        public PointValue Clone()
        {
            var res = new PointValue() { Timestamp = Timestamp };
            if (Fields != null)
            {
                res.Fields = new Dictionary<string, Field>();
                foreach (var elem in Fields)
                {
                    res.Fields.Add(elem.Key, elem.Value.Clone());
                }
            }
            return res;
        }

        // Mutates `this` but not `older`.
        // WARNING: `this` aliases `older`.
        public void MergeWithOlder(PointValue older)
        {
            Condition.Requires(older.Timestamp, "older.Timestamp").IsLessOrEqual(Timestamp);
            if (older.Fields == null) return;
            if (Fields == null)
            {
                Fields = older.Fields;
                return;
            }
            foreach (var elem in older.Fields)
            {
                Field f;
                if (Fields.TryGetValue(elem.Key, out f) && f != null)
                {
                    f.MergeWithOlder(elem.Value);
                }
                else
                {
                    Fields[elem.Key] = elem.Value;
                }
            }
        }
    }

    public class Point
    {
        public PointKey Key { get; set; }
        public PointValue Value { get; set; }
    }

    public interface ISink
    {
        // The caller must not mutate `p`. ISink may mutate it.
        void Push(Point p);
    }

    public interface IBackend
    {
        // Timeout equal to TimeSpan.FromMilliseconds(-1) means infinity.
        Task Send(List<Point> points, TimeSpan timeout);
    }
}
