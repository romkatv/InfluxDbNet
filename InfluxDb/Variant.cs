using System;

namespace InfluxDb
{
    /// <summary>
    /// Sum type: either T0, T1, T2 or T3.
    /// </summary>
    public abstract class Variant<T0, T1, T2, T3> : IEquatable<Variant<T0, T1, T2, T3>>
    {
        public static Variant<T0, T1, T2, T3> New(T0 val)
        {
            return new C0() { Val = val };
        }
        public static Variant<T0, T1, T2, T3> New(T1 val)
        {
            return new C1() { Val = val };
        }
        public static Variant<T0, T1, T2, T3> New(T2 val)
        {
            return new C2() { Val = val };
        }
        public static Variant<T0, T1, T2, T3> New(T3 val)
        {
            return new C3() { Val = val };
        }

        /// <summary>
        /// Calls exactly one of the passed functions passing it the currently held value.
        /// </summary>
        public abstract R Visit<R>(Func<T0, R> v0, Func<T1, R> v1, Func<T2, R> v2, Func<T3, R> v3);

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
            return Visit((T0) => typeof(T0), (T1) => typeof(T1), (T2) => typeof(T2), (T3) => typeof(T3));
        }

        /// <summary>
        /// Currently active value as Object.
        /// </summary>
        public object Value()
        {
            return Visit<object>((T0 v) => v, (T1 v) => v, (T2 v) => v, (T3 v) => v);
        }

        public override string ToString()
        {
            return String.Format("{0}: {1}", Type().FullName, Value());
        }

        public bool Equals(Variant<T0, T1, T2, T3> other)
        {
            if (other == null) return false;
            if (Index() != other.Index()) return false;
            return Object.Equals(Value(), other.Value());
        }

        public override bool Equals(Object obj)
        {
            return Equals(obj as Variant<T0, T1, T2, T3>);
        }

        public override int GetHashCode()
        {
            return Hash.HashWithSeed(Index(), Value());
        }

        sealed class C0 : Variant<T0, T1, T2, T3>
        {
            public T0 Val;
            public override R Visit<R>(Func<T0, R> v0, Func<T1, R> v1, Func<T2, R> v2, Func<T3, R> v3)
            {
                return v0.Invoke(Val);
            }
        }
        sealed class C1 : Variant<T0, T1, T2, T3>
        {
            public T1 Val;
            public override R Visit<R>(Func<T0, R> v0, Func<T1, R> v1, Func<T2, R> v2, Func<T3, R> v3)
            {
                return v1.Invoke(Val);
            }
        }
        sealed class C2 : Variant<T0, T1, T2, T3>
        {
            public T2 Val;
            public override R Visit<R>(Func<T0, R> v0, Func<T1, R> v1, Func<T2, R> v2, Func<T3, R> v3)
            {
                return v2.Invoke(Val);
            }
        }
        sealed class C3 : Variant<T0, T1, T2, T3>
        {
            public T3 Val;
            public override R Visit<R>(Func<T0, R> v0, Func<T1, R> v1, Func<T2, R> v2, Func<T3, R> v3)
            {
                return v3.Invoke(Val);
            }
        }
    }
}
