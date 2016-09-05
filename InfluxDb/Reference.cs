using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace InfluxDb
{
    public struct Reference<T> : IEquatable<Reference<T>> where T : class
    {
        static readonly ConditionalWeakTable<T, object> _sidekicks =
            new ConditionalWeakTable<T, object>();

        public readonly T Value;

        public Reference(T value)
        {
            Value = value;
        }

        public override string ToString()
        {
            return Value == null ? "null" : Value.ToString();
        }

        public bool Equals(Reference<T> other)
        {
            return Object.ReferenceEquals(Value, other.Value);
        }

        public override bool Equals(Object obj)
        {
            return obj is Reference<T> && Equals((Reference<T>)obj);
        }

        public override int GetHashCode()
        {
            if (Value == null) return 73388878;  // Random number.
            return _sidekicks.GetOrCreateValue(Value).GetHashCode();
        }
    }
}
