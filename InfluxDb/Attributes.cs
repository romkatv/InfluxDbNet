using Conditions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InfluxDb
{
    public class Tag : Attribute { };

    public class Name : Attribute
    {
        public Name(string name)
        {
            Condition.Requires(name, "name").IsNotNullOrEmpty();
            Value = name;
        }

        public string Value { get; private set; }
    };

    public class Aggregated : Attribute
    {
        public Aggregated(Aggregation aggregation)
        {
            Aggregation = aggregation;
        }

        public Aggregation Aggregation { get; private set; }
    }
}
