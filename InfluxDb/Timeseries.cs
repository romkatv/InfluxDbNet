using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InfluxDb
{
    public static class Timeseries
    {
        public static void Push<TColumns>(string name, TColumns cols) { }
        public static void Push<TColumns>(string name, TColumns cols, DateTime t) { }
        public static IDisposable At(DateTime t) { return null; }

        public static void SetSink(ISink sink) { }
    }

    class ReplaceableSink : ISink
    {
        readonly Synchronized<ISink> _sink;

        public ReplaceableSink(ISink sink)
        {
            _sink = new Synchronized<ISink>(sink);
        }

        public void SetSink(ISink sink) { _sink.Value = sink; }

        public void Push(Point p)
        {
            ISink sink = _sink.Value;
            if (sink != null) sink.Push(p);
        }
    }
}
