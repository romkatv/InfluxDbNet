using Conditions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InfluxDb
{
    // Fields and properties marked with this attribute become tags in InfluxDb.
    // See https://docs.influxdata.com/influxdb/v1.0/concepts/glossary/#tag.
    //
    //   class Perf {
    //     [Tag]
    //     public string Host { get; set; }
    //
    //     public double CpuLoad { get; set; }
    //   };
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public class TagAttribute : Attribute { };

    // When applied to properties and fields, overrides the name of the tag/field in InfluxDb.
    // When applied to classes and structs, overrides the name of the metric in InfluxDb.
    //
    //   [Name("performance")]
    //   class Perf {
    //     [Name("cpu")]
    //     public double CpuLoad { get; set; }
    //   };
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Class | AttributeTargets.Struct)]
    public class NameAttribute : Attribute
    {
        public NameAttribute(string name)
        {
            Condition.Requires(name, "name").IsNotNullOrEmpty();
            Value = name;
        }

        public string Value { get; private set; }
    };

    // When points get deleted due to down-sampling or overfull buffer, fields (but not tags) get
    // aggregated. This attribute can be used to specify the aggregation algorithm. The default is `Last`.
    //
    //   class Perf {
    //     [Aggregated(Aggregation.Sum)]
    //     public int PageFaults { get; set; }
    //   };
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public class AggregatedAttribute : Attribute
    {
        public AggregatedAttribute(Aggregation aggregation)
        {
            Aggregation = aggregation;
        }

        public Aggregation Aggregation { get; private set; }
    }


    // Shortcuts for different agregation types.
    public class LastAttribute : AggregatedAttribute
    {
        public LastAttribute() : base(Aggregation.Last) { }
    }

    public class SumAttribute : AggregatedAttribute
    {
        public SumAttribute() : base(Aggregation.Sum) { }
    }

    public class MaxAttribute : AggregatedAttribute
    {
        public MaxAttribute() : base(Aggregation.Max) { }
    }

    public class MinAttribute : AggregatedAttribute
    {
        public MinAttribute() : base(Aggregation.Min) { }
    }

    // When applied to properties and fields, instructs the library to ignore them.
    //
    //   class Perf {
    //     [Ignore]
    //     public double NumCores { get; set; }
    //     public double CpuLoad { get; set; }
    //   };
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public class IgnoreAttribute : Attribute { };
}
