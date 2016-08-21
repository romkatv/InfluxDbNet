using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InfluxDb
{
    public abstract class Field : Variant<long, double, bool, string>
    {
        public static new Field New(long val)
        {
            return new Instance(Variant<long, double, bool, string>.New(val));
        }
        public static new Field New(double val)
        {
            return new Instance(Variant<long, double, bool, string>.New(val));
        }
        public static new Field New(bool val)
        {
            return new Instance(Variant<long, double, bool, string>.New(val));
        }
        public static new Field New(string val)
        {
            return new Instance(Variant<long, double, bool, string>.New(val));
        }

        sealed class Instance : Field
        {
            readonly Variant<long, double, bool, string> _var;
            public Instance(Variant<long, double, bool, string> var)
            {
                _var = var;
            }
            public override R Visit<R>(Func<long, R> v0, Func<double, R> v1, Func<bool, R> v2, Func<string, R> v3)
            {
                return _var.Visit(v0, v1, v2, v3);
            }
        }
    }


    public class Point
    {
        public string Name { get; set; }
        public DateTime Timestamp { get; set; }
        public SortedDictionary<string, string> Tags { get; set; }
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
