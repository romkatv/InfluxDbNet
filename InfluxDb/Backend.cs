using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InfluxDb
{
    public class Instance
    {
        public string Endpoint { get; set; }
        public string Database { get; set; }
    }

    public class RestBackend : IBackend, IDisposable
    {
        public RestBackend(Instance instance, TimeSpan timeout) { }
        public Task Send(IEnumerable<Point> points, TimeSpan timeout) { return null; }
        public void Dispose() { }
    }
}
