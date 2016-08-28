using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Conditions;
using System.Linq.Expressions;

using OnTag = System.Action<string, string>;
using OnField = System.Action<string, InfluxDb.Field>;
using E = System.Linq.Expressions.Expression;

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

    public class FieldExtractor
    {
        readonly List<Action<object, OnTag, OnField>> _fields = new List<Action<object, OnTag, OnField>>();

        public FieldExtractor(Type t) : this(t, new Dictionary<Type, FieldExtractor>()) { }

        FieldExtractor(Type t, Dictionary<Type, FieldExtractor> cache)
        {
            Condition.Requires(t, "t").IsNotNull();
            Condition.Requires(cache, "cache").IsNotNull();
            cache.Add(t, this);

            // TODO: t.GetProperties()
            // Extract public static and instance fields.
            FieldInfo[] fields = t.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
            foreach (FieldInfo field in fields)
            {
                string name = field.GetCustomAttribute<Key>()?.Name;
                if (name == null) name = Strings.CamelCaseToUnderscores(field.Name);
                if (field.GetCustomAttribute<Tag>() != null)
                {
                    if (IsNullable(field.FieldType))
                    {
                        ParameterExpression param = E.Parameter(typeof(object), "p");
                        // (object p) => ((Nullable<T>)p).HasValue
                        Func<object, bool> has =
                            E.Lambda<Func<object, bool>>(E.Call(E.Convert(param, field.FieldType), "get_HasValue", null), param).Compile();
                        // (object p) => p.ToString()
                        Func<object, string> get =
                            E.Lambda<Func<object, string>>(E.Call(param, "ToString", null), param).Compile();
                        _fields.Add((obj, onTag, onField) =>
                        {
                            object opt = field.GetValue(obj);
                            if (has(opt)) onTag(name, get(opt));
                        });
                        continue;
                    }
                    _fields.Add((obj, onTag, onField) =>
                    {
                        object f = field.GetValue(obj);
                        if (f != null) onTag(name, f.ToString());
                    });
                    continue;
                }
                // Native field types.
                if (field.FieldType == typeof(long))
                {
                    _fields.Add((obj, onTag, onField) => onField(name, Field.New((long)field.GetValue(obj))));
                    continue;
                }
                if (field.FieldType == typeof(double))
                {
                    _fields.Add((obj, onTag, onField) => onField(name, Field.New((double)field.GetValue(obj))));
                    continue;
                }
                if (field.FieldType == typeof(bool))
                {
                    _fields.Add((obj, onTag, onField) => onField(name, Field.New((bool)field.GetValue(obj))));
                    continue;
                }
                if (field.FieldType == typeof(string))
                {
                    _fields.Add((obj, onTag, onField) =>
                    {
                        object f = field.GetValue(obj);
                        if (f != null) onField(name, Field.New((string)f));
                    });
                    continue;
                }
                // Extra field types that can be trivially converted to native types.
                if (field.FieldType == typeof(short))
                {
                    _fields.Add((obj, onTag, onField) => onField(name, Field.New((short)field.GetValue(obj))));
                    continue;
                }
                if (field.FieldType == typeof(int))
                {
                    _fields.Add((obj, onTag, onField) => onField(name, Field.New((int)field.GetValue(obj))));
                    continue;
                }
                if (field.FieldType == typeof(float))
                {
                    _fields.Add((obj, onTag, onField) => onField(name, Field.New((float)field.GetValue(obj))));
                    continue;
                }
                if (!IsNullable(field.FieldType))
                {
                    FieldExtractor extractor;
                    if (!cache.TryGetValue(field.FieldType, out extractor))
                    {
                        extractor = new FieldExtractor(field.FieldType, cache);
                    }
                    _fields.Add((obj, onTag, onField) =>
                    {
                        object f = field.GetValue(obj);
                        if (f != null) extractor.Extract(f, onTag, onField);
                    });
                    continue;
                }
            }
        }

        public void Extract(object obj, OnTag onTag, OnField onField)
        {
            Condition.Requires(obj, "obj").IsNotNull();
            Condition.Requires(onTag, "onTag").IsNotNull();
            Condition.Requires(onField, "onField").IsNotNull();
            foreach (var f in _fields)
            {
                f(obj, onTag, onField);
            }
        }

        static bool IsNullable(Type t)
        {
            return t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Nullable<>);
        }
    }
}
