using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InfluxDb
{
    public abstract class Field : IEquatable<Field>
    {
        public static Field New(long val)
        {
            return new C0() { Val = val };
        }
        public static Field New(double val)
        {
            return new C1() { Val = val };
        }
        public static Field New(bool val)
        {
            return new C2() { Val = val };
        }
        public static Field New(string val)
        {
            return new C3() { Val = val };
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

        sealed class C0 : Field
        {
            public long Val;
            public override R Visit<R>(Func<long, R> v0, Func<double, R> v1, Func<bool, R> v2, Func<string, R> v3)
            {
                return v0.Invoke(Val);
            }
        }
        sealed class C1 : Field
        {
            public double Val;
            public override R Visit<R>(Func<long, R> v0, Func<double, R> v1, Func<bool, R> v2, Func<string, R> v3)
            {
                return v1.Invoke(Val);
            }
        }
        sealed class C2 : Field
        {
            public bool Val;
            public override R Visit<R>(Func<long, R> v0, Func<double, R> v1, Func<bool, R> v2, Func<string, R> v3)
            {
                return v2.Invoke(Val);
            }
        }
        sealed class C3 : Field
        {
            public string Val;
            public override R Visit<R>(Func<long, R> v0, Func<double, R> v1, Func<bool, R> v2, Func<string, R> v3)
            {
                return v3.Invoke(Val);
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
        Task Send(List<Point> points, TimeSpan timeout);
    }
}
