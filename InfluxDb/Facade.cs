using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InfluxDb
{
    public class Measurement<TColumns>
    {
        internal Measurement(string name, IClock clock, ISink sink) { }
        public void Report(TColumns cols) { }
        public void Report(TColumns cols, DateTime t) { }
    }

    public class Database
    {
        public Database(ISink sink) { }
        public Measurement<TColumns> GetMeasurement<TColumns>(string name) { return null; }
        public IDisposable At(DateTime t) { return null; }
    }

    interface IClock
    {
        DateTime UtcNow { get; }
    }
}
