using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InfluxDb
{
    public static class Reporting
    {
        public static Measurement<TColumns> GetMeasurement<TColumns>(string name) { return null; }
        public static IDisposable At(DateTime t) { return null; }

        static void SetSink(ISink sink) { }
    }

    class ReplaceableSink : ISink
    {
        readonly Synchronized<ISink> _sink;

        public ReplaceableSink(ISink sink)
        {
            _sink = new Synchronized<ISink>(sink);
        }

        public void SetSink(ISink sink) { _sink.Value = sink; }

        public void Write(Point p)
        {
            ISink sink = _sink.Value;
            if (sink != null) sink.Write(p);
        }
    }
}
