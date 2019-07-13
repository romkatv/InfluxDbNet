using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Conditions;
using System.Linq.Expressions;

using OnTag = System.Action<object, int, string>;
using OnField = System.Action<object, int, InfluxDb.Field>;
using E = System.Linq.Expressions.Expression;

namespace InfluxDb {
  static class MeasurementExtractor<T> {
    public static readonly string Name =
        typeof(T).GetCustomAttribute<NameAttribute>()?.Value ??
        Strings.CamelCaseToUnderscores(typeof(T).Name).ToLower();
  }

  class MemberExtractor {
    struct CompositeExtractor {
      public MemberExtractor Extractor { get; set; }
      public Func<object, object> Get { get; set; }
    }

    struct TagExtractor {
      public int Idx { get; set; }
      public Func<object, string> Get { get; set; }
    }

    struct FieldExtractor {
      public int Idx { get; set; }
      public Func<object, Field> Get { get; set; }
    }

    readonly List<CompositeExtractor> _composites = new List<CompositeExtractor>();
    readonly List<TagExtractor> _tags = new List<TagExtractor>();
    readonly List<FieldExtractor> _fields = new List<FieldExtractor>();

    public MemberExtractor(Type t) : this(t, new Dictionary<Type, MemberExtractor>()) { }

    MemberExtractor(Type t, Dictionary<Type, MemberExtractor> cache) {
      Condition.Requires(t, nameof(t)).IsNotNull();
      Condition.Requires(cache, nameof(cache)).IsNotNull();
      cache.Add(t, this);

      var flags = BindingFlags.FlattenHierarchy | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
      // Extract public static and instance fields.
      foreach (FieldInfo x in t.GetFields(flags)) {
        AddExtractor(x, t, cache);
      }
      // Extract public static and instance properties.
      foreach (PropertyInfo x in t.GetProperties(flags)) {
        AddExtractor(x, t, cache);
      }
    }

    void AddExtractor(MemberInfo member, Type container, Dictionary<Type, MemberExtractor> cache) {
      if (member.GetCustomAttribute<IgnoreAttribute>() != null) return;

      Type inner;
      if (member is PropertyInfo prop) {
        if (!prop.CanRead) return;
        inner = prop.PropertyType;
      } else {
        var field = (FieldInfo)member;
        inner = field.FieldType;
      }

      if (member.GetCustomAttribute<TagAttribute>() == null) {
        AddFieldExtractor(container, inner, member, cache);
      } else {
        if (member.GetCustomAttribute<AggregatedAttribute>() != null) {
          throw new Exception($"Attributes Tag and Aggregated are incompatible: {container.Name}.{member.Name}");
        }
        AddTagExtractor(container, inner, member);
      }
    }

    void AddTagExtractor(Type container, Type field, MemberInfo member) {
      _tags.Add(new TagExtractor() {
        Idx = NameTable.Tags.Intern(Name(member)),
        Get = Getter<string>(member.Name, container, field, (E x) => E.Call(x, "ToString", null)),
      });
    }

    void AddFieldExtractor(Type container, Type field, MemberInfo member,
                           Dictionary<Type, MemberExtractor> cache) {
      // Simple field type.
      Type simple = FieldType(ValueType(field));
      if (simple != null) {
        _fields.Add(new FieldExtractor() {
          Idx = NameTable.Fields.Intern(Name(member)),
          Get = Getter<Field>(member.Name, container, field, (E x) =>
            E.Call(typeof(Field), "New", null, E.Convert(x, simple), E.Constant(Aggregation(member))))
        });
        return;
      }

      // Composite type.
      if (ValueType(field).IsPrimitive) {
        throw new Exception($"Unsupported field type: {field.Name} {container.Name}.{member.Name}");
      }
      if (!cache.TryGetValue(ValueType(field), out MemberExtractor extractor)) {
        extractor = new MemberExtractor(ValueType(field), cache);
      }
      _composites.Add(new CompositeExtractor() {
        Extractor = extractor,
        Get = Getter<object>(member.Name, container, field, (E x) => x)
      });
    }

    public void Extract(object obj, object payload, OnTag onTag, OnField onField) {
#if DEBUG
      Condition.Requires(obj, nameof(obj)).IsNotNull();
      Condition.Requires(onTag, nameof(onTag)).IsNotNull();
      Condition.Requires(onField, nameof(onField)).IsNotNull();
#endif
      for (int i = 0, e = _composites.Count; i != e; ++i) {
        CompositeExtractor x = _composites[i];
        object v = x.Get(obj);
        if (v != null) x.Extractor.Extract(v, payload, onTag, onField);
      }
      for (int i = 0, e = _tags.Count; i != e; ++i) {
        TagExtractor x = _tags[i];
        string v = x.Get(obj);
        if (v != null) onTag(payload, x.Idx, v);
      }
      for (int i = 0, e = _fields.Count; i != e; ++i) {
        FieldExtractor x = _fields[i];
        Field v = x.Get(obj);
        if (v != null) onField(payload, x.Idx, v);
      }
    }

    static Func<object, T> Getter<T>(string name, Type container, Type member, Func<E, E> convert) {
      ParameterExpression obj = E.Parameter(typeof(object), "obj");
      BindingFlags flags = Flags(container, name);
      E x;
      if (flags.HasFlag(BindingFlags.Instance)) {
        // E.PropertyOrField() doesn't work for static properties and fields.
        x = E.PropertyOrField(E.Convert(obj, container), name);
      } else if (flags.HasFlag(BindingFlags.GetField)) {
        x = E.Field(null, container, name);
      } else {
        Condition.Requires(flags.HasFlag(BindingFlags.GetProperty)).IsTrue();
        x = E.Property(null, container, name);
      }

      E nul = E.Convert(E.Constant(null), typeof(T));
      if (IsNullable(member)) {
        x = E.Condition(E.Property(x, "HasValue"), convert.Invoke(E.Property(x, "Value")), nul);
      } else if (member.IsClass) {
        x = E.Condition(E.NotEqual(x, E.Constant(null)), convert.Invoke(x), nul);
      } else {
        x = convert.Invoke(x);
      }
      return E.Lambda<Func<object, T>>(x, obj).Compile();
    }

    static BindingFlags Flags(Type t, string name) {
      foreach (var f in new[] { BindingFlags.Instance, BindingFlags.Static }) {
        MemberInfo[] m = t.GetMember(name, BindingFlags.FlattenHierarchy | BindingFlags.Public | f);
        if (m != null && m.Length > 0) {
          Condition.Requires(m.Length, nameof(m.Length)).IsEqualTo(1);
          if (m[0].MemberType == MemberTypes.Field) return f | BindingFlags.GetField;
          if (m[0].MemberType == MemberTypes.Property) return f | BindingFlags.GetProperty;
          throw new Exception("Unexpected member type: " + m[0].MemberType);
        }
      }
      throw new Exception(t.Name + " doesn't have member " + name);
    }

    static string Name(MemberInfo member) {
      return member.GetCustomAttribute<NameAttribute>()?.Value ??
          Strings.CamelCaseToUnderscores(member.Name).ToLower();
    }

    static Aggregation Aggregation(MemberInfo member) {
      var attr = member.GetCustomAttribute<AggregatedAttribute>();
      return attr == null ? InfluxDb.Aggregation.Last : attr.Aggregation;
    }

    static bool IsNullable(Type t) {
      return t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Nullable<>);
    }

    static Type ValueType(Type t) {
      return IsNullable(t) ? t.GetGenericArguments().First() : t;
    }

    static Type FieldType(Type f) {
      if (f == typeof(long) || f == typeof(double) || f == typeof(bool) || f == typeof(string)) {
        return f;
      }
      if (f == typeof(byte) || f == typeof(sbyte) ||
          f == typeof(short) || f == typeof(ushort) ||
          f == typeof(int) || f == typeof(uint)) {
        return typeof(long);
      }
      if (f == typeof(float) || f == typeof(decimal)) {
        return typeof(double);
      }
      return null;
    }
  }

  static class MemberExtractor<TColumns> {
    public static readonly MemberExtractor Instance = new MemberExtractor(typeof(TColumns));
  }
}
