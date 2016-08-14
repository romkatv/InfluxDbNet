using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InfluxDb
{
    class Synchronized<T>
    {
        T _value;
        readonly object _monitor = new object();

        public Synchronized() { }
        public Synchronized(T value) { _value = value; }

        public T Value
        {
            get { lock (_monitor) return _value; }
            set { lock (_monitor) _value = value; }
        }
    }
}
