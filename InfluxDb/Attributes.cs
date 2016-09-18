using Conditions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InfluxDb
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public class Tag : Attribute { };

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Class | AttributeTargets.Struct)]
    public class Name : Attribute
    {
        public Name(string name)
        {
            Condition.Requires(name, "name").IsNotNullOrEmpty();
            Value = name;
        }

        public string Value { get; private set; }
    };

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public class Aggregated : Attribute
    {
        public Aggregated(Aggregation aggregation)
        {
            Aggregation = aggregation;
        }

        public Aggregation Aggregation { get; private set; }
    }

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public class Ignore : Attribute { };
}
