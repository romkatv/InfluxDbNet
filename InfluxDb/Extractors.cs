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
    static class MeasurementExtractor<T>
    {
        public static readonly string Name =
            typeof(T).GetCustomAttribute<Name>()?.Value ??
            Strings.CamelCaseToUnderscores(typeof(T).Name).ToLower();
    }

    class MemberExtractor
    {
        // Composite extractors are at [0].
        // Simple extractors are at [1].
        readonly List<Action<object, OnTag, OnField>>[] _extractors = new [] {
            new List<Action<object, OnTag, OnField>>(),
            new List<Action<object, OnTag, OnField>>()
        };

        public MemberExtractor(Type t) : this(t, new Dictionary<Type, MemberExtractor>()) { }

        MemberExtractor(Type t, Dictionary<Type, MemberExtractor> cache)
        {
            Condition.Requires(t, "t").IsNotNull();
            Condition.Requires(cache, "cache").IsNotNull();
            cache.Add(t, this);

            var flags = BindingFlags.FlattenHierarchy | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
            // Extract public static and instance fields.
            foreach (FieldInfo x in t.GetFields(flags))
            {
                if (x.GetCustomAttribute<Tag>() == null)
                {
                    AddFieldExtractor(Name(x), x.FieldType, Getter(x.Name, t, x.FieldType), cache);
                }
                else
                {
                    AddTagExtractor(Name(x), Getter(x.Name, t, x.FieldType));
                }
            }
            // Extract public static and instance properties.
            foreach (PropertyInfo x in t.GetProperties(flags))
            {
                if (!x.CanRead) continue;
                if (x.GetCustomAttribute<Tag>() == null)
                {
                    AddFieldExtractor(Name(x), x.PropertyType, Getter(x.Name, t, x.PropertyType), cache);
                }
                else
                {
                    AddTagExtractor(Name(x), Getter(x.Name, t, x.PropertyType));
                }
            }
        }

        void AddTagExtractor(string name, Func<object, object> get)
        {
            _extractors[1].Add((obj, onTag, onField) =>
            {
                object x = get(obj);
                if (x != null) onTag(name, x.ToString());
            });
        }

        void AddFieldExtractor(string name, Type t, Func<object, object> get, Dictionary<Type, MemberExtractor> cache)
        {
            // Simple field type.
            Type field = FieldType(ValueType(t));
            if (field != null)
            {
                Func<object, Field> make;
                {
                    ParameterExpression obj = E.Parameter(typeof(object), "obj");
                    E e = E.Call(typeof(Field), "New", null, E.Convert(E.Convert(obj, t), field));
                    make = E.Lambda<Func<object, Field>>(e, obj).Compile();
                }
                _extractors[1].Add((obj, onTag, onField) =>
                {
                    object x = get(obj);
                    if (x != null) onField(name, make(x));
                });
                return;
            }

            // Composite type.
            if (ValueType(t).IsPrimitive) throw new Exception("Unsupported type: " + t.Name);
            MemberExtractor extractor;
            if (!cache.TryGetValue(ValueType(t), out extractor))
            {
                extractor = new MemberExtractor(ValueType(t), cache);
            }
            _extractors[0].Add((obj, onTag, onField) =>
            {
                object x = get(obj);
                if (x != null) extractor.Extract(x, onTag, onField);
            });
        }

        public void Extract(object obj, OnTag onTag, OnField onField)
        {
            Condition.Requires(obj, "obj").IsNotNull();
            Condition.Requires(onTag, "onTag").IsNotNull();
            Condition.Requires(onField, "onField").IsNotNull();
            foreach (var x in _extractors)
            {
                foreach (var y in x)
                {
                    y(obj, onTag, onField);
                }
            }
        }

        static Func<object, object> Getter(string name, Type container, Type member)
        {
            ParameterExpression obj = E.Parameter(typeof(object), "obj");
            BindingFlags flags = Flags(container, name);
            E x;
            if (flags.HasFlag(BindingFlags.Instance))
            {
                // E.PropertyOrField() doesn't work for static properties and fields.
                x = E.PropertyOrField(E.Convert(obj, container), name);
            }
            else if (flags.HasFlag(BindingFlags.GetField))
            {
                x = E.Field(null, container, name);
            }
            else
            {
                Condition.Requires(flags.HasFlag(BindingFlags.GetProperty)).IsTrue();
                x = E.Property(null, container, name);
            }
            if (IsNullable(member))
            {
                x = E.Condition(E.Property(x, "HasValue"), E.Convert(E.Property(x, "Value"), typeof(object)), E.Constant(null));
            }
            else
            {
                x = E.Convert(x, typeof(object));
            }
            return E.Lambda<Func<object, object>>(x, obj).Compile();
        }

        static BindingFlags Flags(Type t, string name)
        {
            foreach (var f in new[] { BindingFlags.Instance, BindingFlags.Static })
            {
                MemberInfo[] m = t.GetMember(name, BindingFlags.FlattenHierarchy | BindingFlags.Public | f);
                if (m != null && m.Length > 0)
                {
                    Condition.Requires(m.Length, "m.Length").IsEqualTo(1);
                    if (m[0].MemberType == MemberTypes.Field) return f | BindingFlags.GetField;
                    if (m[0].MemberType == MemberTypes.Property) return f | BindingFlags.GetProperty;
                    throw new Exception("Unexpected member type: " + m[0].MemberType);
                }
            }
            throw new Exception(t.Name + " doesn't have member " + name);
        }

        static string Name(MemberInfo member)
        {
            return member.GetCustomAttribute<Name>()?.Value ??
                Strings.CamelCaseToUnderscores(member.Name).ToLower();
        }

        static bool IsNullable(Type t)
        {
            return t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Nullable<>);
        }

        static Type ValueType(Type t)
        {
            return IsNullable(t) ? t.GetGenericArguments().First() : t;
        }

        static Type FieldType(Type f)
        {
            if (f == typeof(long) || f == typeof(double) || f == typeof(bool) || f == typeof(string))
            {
                return f;
            }
            if (f == typeof(byte) || f == typeof(sbyte) ||
                f == typeof(short) || f == typeof(ushort) ||
                f == typeof(int) || f == typeof(uint))
            {
                return typeof(long);
            }
            if (f == typeof(float))
            {
                return typeof(double);
            }
            return null;
        }
    }

    static class MemberExtractor<TColumns>
    {
        public static readonly MemberExtractor Instance = new MemberExtractor(typeof(TColumns));
    }
}
