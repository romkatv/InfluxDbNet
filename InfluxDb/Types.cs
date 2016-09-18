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
            return Visit((T0) => 0, (T1) => 1, (T2) => 2, (T3) => 3);
        }

        /// <summary>
        /// Type of the active value.
        /// </summary>
        public Type Type()
        {
            return Visit((T0) => typeof(long), (T1) => typeof(double), (T2) => typeof(bool), (T3) => typeof(string));
        }

        /// <summary>
        /// Currently active value as Object.
        /// </summary>
        public object Value()
        {
            return Visit<object>((long v) => v, (double v) => v, (bool v) => v, (string v) => v);
        }

        public void MergeFrom(Field other)
        {
            Condition.Requires(other, "other").IsNotNull();
            if (GetType() != other.GetType())
            {
                throw new ArgumentException(string.Format(
                        "Can't merge fields of different types: {0} vs {1}",
                        Type().Name, other.Type().Name));
            }
            if (_aggregation != other._aggregation)
            {
                throw new ArgumentException(string.Format(
                    "Aggregation function must be the same for two values to be mergeable: {0} vs {1}",
                    _aggregation, other._aggregation));
            }
            MergeFromImpl(other);
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

        protected abstract void MergeFromImpl(Field other);

        sealed class C0 : Field
        {
            public long Val;

            public override R Visit<R>(Func<long, R> v0, Func<double, R> v1, Func<bool, R> v2, Func<string, R> v3)
            {
                return v0.Invoke(Val);
            }

            protected override void MergeFromImpl(Field other)
            {
                switch (_aggregation)
                {
                    case Aggregation.Last:
                        break;
                    case Aggregation.Sum:
                        Val += ((C0)other).Val;
                        break;
                    case Aggregation.Min:
                        Val = Math.Min(Val, ((C0)other).Val);
                        break;
                    case Aggregation.Max:
                        Val = Math.Min(Val, ((C0)other).Val);
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

            protected override void MergeFromImpl(Field other)
            {
                switch (_aggregation)
                {
                    case Aggregation.Last:
                        break;
                    case Aggregation.Sum:
                        Val += ((C1)other).Val;
                        break;
                    case Aggregation.Min:
                        Val = Math.Min(Val, ((C1)other).Val);
                        break;
                    case Aggregation.Max:
                        Val = Math.Min(Val, ((C1)other).Val);
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

            protected override void MergeFromImpl(Field other)
            {
                switch (_aggregation)
                {
                    case Aggregation.Last:
                        break;
                    case Aggregation.Sum:
                        Val = Val || ((C2)other).Val;
                        break;
                    case Aggregation.Min:
                        Val = Val && ((C2)other).Val;
                        break;
                    case Aggregation.Max:
                        Val = Val || ((C2)other).Val;
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

            protected override void MergeFromImpl(Field other)
            {
                string s = ((C3)other).Val;
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
