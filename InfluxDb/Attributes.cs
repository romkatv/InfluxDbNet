using Conditions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InfluxDb
{
    public class Tag : Attribute { };

    public class Key : Attribute
    {
        public Key(string name)
        {
            Condition.Requires(name, "name").IsNotNullOrEmpty();
            Name = name;
        }

        public string Name { get; private set; }
    };

    public class Measurement : Attribute
    {
        public Measurement(string name)
        {
            Condition.Requires(name, "name").IsNotNullOrEmpty();
            Name = name;
        }

        public string Name { get; private set; }
    };
}
