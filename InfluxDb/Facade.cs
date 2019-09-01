using Conditions;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using Tags = System.Collections.Generic.Dictionary<string, string>;
using Fields = System.Collections.Generic.Dictionary<string, InfluxDb.Field>;

namespace InfluxDb {
  public class Facade {
    static volatile Facade _instance;

    readonly MultiThreadedOverrides _global = new MultiThreadedOverrides();
    readonly ThreadLocal<ShardedPoint> _local = new ThreadLocal<ShardedPoint>(() => new ShardedPoint());
    readonly ISink _sink;

    public Facade(ISink sink) {
      Condition.Requires(sink, nameof(sink)).IsNotNull();
      _sink = sink;
    }

    // Name can be null, in which case it is extracted from `TColumns`.
    public void Push<TColumns>(string name, DateTime t, in TColumns cols) {
      ShardedPoint p = GetBase();
      Extract(cols, p.Pools, p.Final);
      p.Final.Timestamp = t;
      DoPush(name ?? MeasurementExtractor<TColumns>.Name, p);
    }

    // Name can be null, in which case it is extracted from `TColumns`.
    public void Push<TColumns>(string name, in TColumns cols) {
      ShardedPoint p = GetBase();
      Extract(cols, p.Pools, p.Final);
      DoPush(name ?? MeasurementExtractor<TColumns>.Name, p);
    }

    public void Push<TColumns>(DateTime t, in TColumns cols) => Push(null, t, cols);

    public void Push<TColumns>(in TColumns cols) => Push(null, cols);

    // Name and point cannot be null.
    public void Push(string name, PartialPoint final) {
      ShardedPoint p = GetBase();
      p.Final.CopyFrom(p.Pools, final);
      p.Final = final;
      DoPush(name, p);
    }

    // Thread-local override.
    //
    // Dispose() on the returned object must be called on the same thread.
    public IDisposable At(DateTime t) {
      ShardedPoint p = _local.Value;
      var node = new PartialPoint() { Timestamp = t };
      p.Local.AddLast(node);
      return new SingleThreadedDeleter(p, node);
    }

    // Thread-local override.
    //
    // Dispose() on the returned object must be called on the same thread.
    public IDisposable With<TColumns>(DateTime t, in TColumns cols) {
      ShardedPoint p = _local.Value;
      var node = new PartialPoint() { Timestamp = t };
      Extract(cols, p.Pools, node);
      p.Local.AddLast(node);
      return new SingleThreadedDeleter(p, node);
    }

    // Thread-local override.
    //
    // Dispose() on the returned object must be called on the same thread.
    public IDisposable With<TColumns>(in TColumns cols) {
      ShardedPoint p = _local.Value;
      var node = new PartialPoint();
      Extract(cols, p.Pools, node);
      p.Local.AddLast(node);
      return new SingleThreadedDeleter(p, node);
    }

    // Thread-local override.
    //
    // Dispose() on the returned object must be called on the same thread.
    public IDisposable With(DateTime t, PartialPoint local) {
      ShardedPoint p = _local.Value;
      p.Local.AddLast(local);
      return new SingleThreadedDeleter(p, local);
    }

    // Thread-local override.
    //
    // Dispose() on the returned object must be called on the same thread.
    public IDisposable With(PartialPoint local) {
      ShardedPoint p = _local.Value;
      // var node = new PartialPoint();
      // node.CopyFrom(p.Pools, local);
      var node = local;
      p.Local.AddLast(node);
      return new SingleThreadedDeleter(p, node);
    }

    public IDisposable GlobalAt(DateTime t) => _global.Add(new PartialPoint() { Timestamp = t });

    public IDisposable GlobalWith<TColumns>(DateTime t, in TColumns cols) {
      ShardedPoint p = _local.Value;
      var node = new PartialPoint() { Timestamp = t };
      Extract(cols, p.Pools, node);
      return _global.Add(node);
    }

    public IDisposable GlobalWith<TColumns>(in TColumns cols) {
      ShardedPoint p = _local.Value;
      var node = new PartialPoint();
      Extract(cols, p.Pools, node);
      return _global.Add(node);
    }

    public IDisposable GlobalWith(PartialPoint global) {
      ShardedPoint p = _local.Value;
      var node = new PartialPoint();
      node.CopyFrom(p.Pools, global);
      return _global.Add(node);
    }

    public static Facade Instance {
      get { return _instance; }
      set { _instance = value; }
    }

    public static PartialPoint Extract<TColumns>(in TColumns cols) {
      var p = new PartialPoint();
      Extract(cols, new Pools(), p);
      return p;
    }

    ShardedPoint GetBase() {
      var res = _local.Value;
      res.Global = _global.Current;
      res.Final.Timestamp = null;
      res.Final.Tags.ResizeUninitialized(0);
      res.Final.Fields.ResizeUninitialized(0);
      return res;
    }

    void DoPush(string name, ShardedPoint p) {
      if (HasFields(p)) _sink.Push(name, p);
    }

    static bool HasFields(ShardedPoint p) {
      if (p.Final.Fields.Count > 0) return true;
      for (PartialPoint node = p.Local.Last; node != null; node = node.Prev) {
        if (node.Fields.Count > 0) return true;
      }
      return p.Global.Fields.Count > 0;
    }

    // Copies tags and fields (but not the name) from `cols` to `p`.
    static void Extract<TColumns>(in TColumns cols, Pools pools, PartialPoint point) {
      MemberExtractor<TColumns>.Instance.Extract(
          pools,
          cols,
          point,
          (object payload, int idx, string tag) => {
            var p = (PartialPoint)payload;
            p.Tags.Add(new Indexed<string>() { Index = idx, Value = tag });
          },
          (object payload, int idx, Field field) => {
            var p = (PartialPoint)payload;
            p.Fields.Add(new Indexed<Field> { Index = idx, Value = field });
          });
    }
  }

  class SingleThreadedDeleter : IDisposable {
    readonly int _thread;
    readonly ShardedPoint _p;
    PartialPoint _node;

    public SingleThreadedDeleter(ShardedPoint p, PartialPoint node) {
      _thread = Thread.CurrentThread.ManagedThreadId;
      _p = p;
      _node = node;
    }

    public void Dispose() {
      Condition.Requires(Thread.CurrentThread.ManagedThreadId, nameof(Thread.CurrentThread.ManagedThreadId))
          .IsEqualTo(_thread, "SingleThreadedDeleter must be disposed from the same thread that created it");
      if (_node != null) {
        for (int i = 0, e = _node.Fields.Count; i != e; ++i) _node.Fields[i].Value.Release(_p.Pools);
        _p.Local.Remove(_node);
        _node = null;
      }
    }
  }

  class MultiThreadedOverrides {
    readonly object _monitor = new object();
    readonly Pools _pools = new Pools();
    PartialPoint _aggregate = new PartialPoint();
    readonly IntrusiveListNode<PartialPoint>.List _list = new IntrusiveListNode<PartialPoint>.List();

    class Deleter : IDisposable {
      readonly MultiThreadedOverrides _outer;
      PartialPoint _node;

      public Deleter(MultiThreadedOverrides outer, PartialPoint node) {
        Condition.Requires(outer, nameof(outer)).IsNotNull();
        Condition.Requires(node, nameof(node)).IsNotNull();
        _outer = outer;
        _node = node;
      }

      public void Dispose() {
        lock (_outer._monitor) {
          if (_node != null) {
            _outer._list.Remove(_node);
            _node = null;
            _outer.Update();
          }
        }
      }
    }

    // The caller must not mutate `p`.
    public IDisposable Add(PartialPoint p) {
      lock (_monitor) {
        _list.AddLast(p);
        Update();
      }
      return new Deleter(this, p);
    }

    public PartialPoint Current => Volatile.Read(ref _aggregate);

    void Update() {
      Condition.Requires(Monitor.IsEntered(_monitor)).IsTrue();
      var tags = new bool[NameTable.Tags.Array.Length];
      var fields = new bool[NameTable.Fields.Array.Length];
      var aggregate = new PartialPoint();
      for (PartialPoint node = _list.Last; node != null; node = node.Prev) MergePoint(node);
      void MergePoint(PartialPoint p) {
        if (!aggregate.Timestamp.HasValue) aggregate.Timestamp = p.Timestamp;
        for (int i = p.Tags.Count - 1; i >= 0; --i) {
          ref Indexed<string> tag = ref p.Tags[i];
          if (tags[tag.Index]) continue;
          tags[tag.Index] = true;
          aggregate.Tags.Add(tag);
        }
        for (int i = p.Fields.Count - 1; i >= 0; --i) {
          ref Indexed<Field> field = ref p.Fields[i];
          if (fields[field.Index]) continue;
          fields[field.Index] = true;
          aggregate.Fields.Add(field);
        }
      }
    }
  }
}
