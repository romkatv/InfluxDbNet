﻿using System;
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

    public class MemberExtractor
    {
        readonly List<Action<object, OnTag, OnField>> _fields = new List<Action<object, OnTag, OnField>>();

        public MemberExtractor(Type t) : this(t, new Dictionary<Type, MemberExtractor>()) { }

        MemberExtractor(Type t, Dictionary<Type, MemberExtractor> cache)
        {
            Condition.Requires(t, "t").IsNotNull();
            Condition.Requires(cache, "cache").IsNotNull();
            cache.Add(t, this);

            var flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
            // Extract public static and instance fields.
            foreach (FieldInfo x in t.GetFields(flags))
            {
                if (x.GetCustomAttribute<Tag>() == null)
                {
                    _fields.Add(FieldExtractor(Name(x), x.FieldType, Getter(x.Name, t, x.FieldType), cache));
                }
                else
                {
                    _fields.Add(TagExtractor(Name(x), Getter(x.Name, t, x.FieldType)));
                }
            }
            // Extract public static and instance properties.
            foreach (PropertyInfo x in t.GetProperties(flags))
            {
                if (!x.CanRead) continue;
                if (x.GetCustomAttribute<Tag>() == null)
                {
                    _fields.Add(FieldExtractor(Name(x), x.PropertyType, Getter(x.Name, t, x.PropertyType), cache));
                }
                else
                {
                    _fields.Add(TagExtractor(Name(x), Getter(x.Name, t, x.PropertyType)));
                }
            }
        }

        static Action<object, OnTag, OnField> TagExtractor(string name, Func<object, object> get)
        {
            return (obj, onTag, onField) =>
            {
                object x = get(obj);
                if (x != null) onTag(name, x.ToString());
            };
        }

        static Action<object, OnTag, OnField> FieldExtractor(
            string name, Type t, Func<object, object> get, Dictionary<Type, MemberExtractor> cache)
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
                return (obj, onTag, onField) =>
                {
                    object x = get(obj);
                    if (x != null) onField(name, make(x));
                };
            }

            // Composite type.
            MemberExtractor extractor;
            if (!cache.TryGetValue(ValueType(t), out extractor))
            {
                extractor = new MemberExtractor(ValueType(t), cache);
            }
            return (obj, onTag, onField) =>
            {
                object x = get(obj);
                if (x != null) extractor.Extract(x, onTag, onField);
            };
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

        static Func<object, object> Getter(string name, Type container, Type member)
        {
            ParameterExpression obj = E.Parameter(typeof(object), "obj");
            E x = E.PropertyOrField(E.Convert(obj, container), name);
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

        static string Name(MemberInfo member)
        {
            return member.GetCustomAttribute<Key>()?.Name ?? Strings.CamelCaseToUnderscores(member.Name);
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
            if (f == typeof(short) || f == typeof(int))
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
}
