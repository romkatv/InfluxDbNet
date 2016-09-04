using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InfluxDb
{
    public static class Timeseries
    {
        static readonly Synchronized<Facade> _facade = new Synchronized<Facade>();

        public static void Push<TColumns>(string name, TColumns cols)
        {
            _facade.Value?.Push(name, cols);
        }

        public static void Push<TColumns>(string name, TColumns cols, DateTime t)
        {
            _facade.Value?.Push(name, cols, t);
        }

        public static void Push<TColumns>(TColumns cols)
        {
            _facade.Value?.Push(cols);
        }

        public static void Push<TColumns>(TColumns cols, DateTime t)
        {
            _facade.Value?.Push(cols, t);
        }

        public static IDisposable At(DateTime t)
        {
            return _facade.Value?.At(t);
        }

        public static IDisposable With<TColumns>(DateTime t, TColumns cols)
        {
            return _facade.Value?.With(t, cols);
        }

        public static IDisposable With<TColumns>(TColumns cols)
        {
            return _facade.Value?.With(cols);
        }

        public static void SetSink(ISink sink)
        {
            _facade.Value = new Facade(sink);
        }
    }
}
