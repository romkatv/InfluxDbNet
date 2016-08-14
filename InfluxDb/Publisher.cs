using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InfluxDb
{
    public enum OnFull
    {
        DropOldest,
        Downsample,
        Block,
    }

    public class PublisherConfig
    {
        public int MaxStoredPoints { get; set; }
        public int MaxPointsPerBatch { get; set; }
        public OnFull OnFull { get; set; }
        public TimeSpan SendPeriod { get; set; }
        public TimeSpan SendTimeout { get; set; }
    }

    public class Publisher : ISink, IDisposable
    {
        public Publisher(IBackend backend, PublisherConfig cfg) { }

        public void Write(Point p) { }

        public Task Flush(TimeSpan timeout) { return null; }

        public void Dispose() { }
    }
}
