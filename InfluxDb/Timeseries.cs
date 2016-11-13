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

        public static void Push<TColumns>(string name, DateTime t, TColumns cols)
        {
            _facade.Value?.Push(name, t, cols);
        }

        public static void Push<TColumns>(TColumns cols)
        {
            _facade.Value?.Push(cols);
        }

        public static void Push<TColumns>(DateTime t, TColumns cols)
        {
            _facade.Value?.Push(t, cols);
        }

        public static void Push(string name, DateTime t, Point p)
        {
            _facade.Value?.Push(name, t, p);
        }

        public static void Push(DateTime t, Point p)
        {
            _facade.Value?.Push(t, p);
        }

        public static void Push(string name, Point p)
        {
            _facade.Value?.Push(name, p);
        }

        public static void Push(Point p)
        {
            _facade.Value?.Push(p);
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

        public static IDisposable With(DateTime t, Point p)
        {
            return _facade.Value?.With(t, p);
        }

        public static IDisposable With(Point p)
        {
            return _facade.Value?.With(p);
        }

        public static void SetSink(ISink sink)
        {
            _facade.Value = new Facade(sink);
        }

        public static Point MaybeExtract<TColumns>(TColumns cols)
        {
            if (_facade.Value == null) return null;
            return Facade.Extract(cols);
        }

        public static Point Extract<TColumns>(TColumns cols)
        {
            return Facade.Extract(cols);
        }
    }
}
