using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using OnTag = System.Action<string, string>;
using OnField = System.Action<string, InfluxDb.Field>;
using Conditions;
using System.Linq.Expressions;

namespace InfluxDb
{
    public class Tag : Attribute { };

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
                // TODO: support overrides via attributes.
                string name = Strings.CamelCaseToUnderscores(field.Name);
                if (field.GetCustomAttribute<Tag>() != null)
                {
                    if (IsNullable(field.FieldType))
                    {
                        Type v = field.FieldType.GetGenericArguments().First();
                        ParameterExpression self = Expression.Parameter(v, "this");
                        Func<object, object> has =
                            Expression.Lambda<Func<object, object>>(
                                Expression.Call(self, v.GetProperty("HasValue").GetMethod), self).Compile();
                        Func<object, object> get =
                            Expression.Lambda<Func<object, object>>(
                                Expression.Call(self, v.GetProperty("Value").GetMethod), self).Compile();
                        _fields.Add((obj, onTag, onField) =>
                        {
                            object opt = field.GetValue(obj);
                            if ((bool)has(opt)) onTag(name, get(opt).ToString());
                        });
                        continue;
                    }
                    _fields.Add((obj, onTag, onField) => onTag(name, field.GetValue(obj).ToString()));
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
                        if (f != null) extractor.Extract(obj, onTag, onField);
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
