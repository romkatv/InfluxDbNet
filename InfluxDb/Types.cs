using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InfluxDb
{
    public class Tag
    {
        public string Name { get; set; }
        public string Value { get; set; }
    }

    public class Field
    {
        public string Name { get; set; }
        public double Value { get; set; }
    }

    public class Point
    {
        public string Name { get; set; }
        public SortedDictionary<string, Tag> Tags { get; set; }
        public SortedDictionary<string, Field> Fields { get; set; }
    }

    public interface ISink
    {
        void Write(Point p);
    }

    public interface IBackend
    {
        Task Send(List<Point> points, TimeSpan timeout);
    }
}
