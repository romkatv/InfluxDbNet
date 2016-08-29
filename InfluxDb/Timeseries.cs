using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InfluxDb
{
    public static class Timeseries
    {
        static readonly ReplaceableSink _sink = new ReplaceableSink(null);
        static readonly Facade _facade = new Facade(_sink);

        public static void Push<TColumns>(string name, TColumns cols)
        {
            _facade.Push(name, cols);
        }

        public static void Push<TColumns>(string name, TColumns cols, DateTime t)
        {
            _facade.Push(name, cols, t);
        }

        public static IDisposable At(DateTime t)
        {
            return _facade.At(t);
        }

        public static void SetSink(ISink sink)
        {
            _sink.SetSink(sink);
        }
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
