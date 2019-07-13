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

    readonly MultiThreadedOverrides _globalOverrides = new MultiThreadedOverrides();
    readonly ThreadLocal<SingleThreadedOverrides> _threadLocalOverrides =
        new ThreadLocal<SingleThreadedOverrides>(() => new SingleThreadedOverrides());
    readonly ISink _sink;

    public Facade(ISink sink) {
      Condition.Requires(sink, nameof(sink)).IsNotNull();
      _sink = sink;
    }

    // Name can be null, in which case it is extracted from `TColumns`.
    public void Push<TColumns>(string name, DateTime t, TColumns cols) {
      Override x = GetBase();
      Extract(cols, x);
      x.Timestamp = t;
      DoPush(name ?? MeasurementExtractor<TColumns>.Name, x);
    }

    // Name can be null, in which case it is extracted from `TColumns`.
    public void Push<TColumns>(string name, TColumns cols) {
      Override x = GetBase();
      Extract(cols, x);
      DoPush(name ?? MeasurementExtractor<TColumns>.Name, x);
    }

    public void Push<TColumns>(DateTime t, TColumns cols) => Push(null, t, cols);

    public void Push<TColumns>(TColumns cols) => Push(null, cols);

    // Name and point cannot be null.
    public void Push(string name, DateTime t, PartialPoint p) {
#if DEBUG
      Condition.Requires(p, nameof(p)).IsNotNull();
#endif
      Override x = GetBase();
      x.MergeFrom(t, p);
      DoPush(name, x);
    }

    public void Push(string name, PartialPoint p) {
#if DEBUG
      Condition.Requires(p, nameof(p)).IsNotNull();
#endif
      Override x = GetBase();
      x.MergeFrom(null, p);
      DoPush(name, x);
    }

    // Thread-local override.
    //
    // Dispose() on the returned object must be called on the same thread.
    public IDisposable At(DateTime t) => _threadLocalOverrides.Value.Add(new Override() { Timestamp = t });

    // Thread-local override.
    //
    // Dispose() on the returned object must be called on the same thread.
    public IDisposable With<TColumns>(DateTime t, TColumns cols) {
      var data = new Override() { Timestamp = t };
      Extract(cols, data);
      return _threadLocalOverrides.Value.Add(data);
    }

    // Thread-local override.
    //
    // Dispose() on the returned object must be called on the same thread.
    public IDisposable With<TColumns>(TColumns cols) {
      var data = new Override();
      Extract(cols, data);
      return _threadLocalOverrides.Value.Add(data);
    }

    // Thread-local override.
    //
    // Dispose() on the returned object must be called on the same thread.
    public IDisposable With(DateTime t, PartialPoint p) {
#if DEBUG
      Condition.Requires(p, nameof(p)).IsNotNull();
#endif
      var x = new Override();
      x.MergeFrom(t, p);
      return _threadLocalOverrides.Value.Add(x);
    }

    // Thread-local override.
    //
    // Dispose() on the returned object must be called on the same thread.
    public IDisposable With(PartialPoint p) {
#if DEBUG
      Condition.Requires(p, nameof(p)).IsNotNull();
#endif
      var x = new Override();
      x.MergeFrom(null, p);
      return _threadLocalOverrides.Value.Add(x);
    }

    public IDisposable GlobalAt(DateTime t) => _globalOverrides.Add(new Override() { Timestamp = t });

    public IDisposable GlobalWith<TColumns>(DateTime t, TColumns cols) {
      var data = new Override() { Timestamp = t };
      Extract(cols, data);
      return _globalOverrides.Add(data);
    }

    public IDisposable GlobalWith<TColumns>(TColumns cols) {
      var data = new Override();
      Extract(cols, data);
      return _globalOverrides.Add(data);
    }

    public IDisposable GlobalWith(DateTime t, PartialPoint p) {
#if DEBUG
      Condition.Requires(p, nameof(p)).IsNotNull();
#endif
      var x = new Override();
      x.MergeFrom(t, p);
      return _globalOverrides.Add(x);
    }

    public IDisposable GlobalWith(PartialPoint p) {
#if DEBUG
      Condition.Requires(p, nameof(p)).IsNotNull();
#endif
      var x = new Override();
      x.MergeFrom(null, p);
      return _globalOverrides.Add(x);
    }

    public static Facade Instance {
      get { return _instance; }
      set { _instance = value; }
    }

    // Sets Point.Key.Timestamp to DateTime.MinValue, which has a special meaning. Push() replaces
    // such values with DateTime.UtcNow.
    public static PartialPoint Extract<TColumns>(TColumns cols) {
      var p = new PartialPoint();
      Extract(cols, p);
      return p;
    }

    Override GetBase() {
      Override res = new Override();
      _globalOverrides.MergeTo(res);
      _threadLocalOverrides.Value.MergeTo(res);
      return res;
    }

    // DoPush() stores a reference to `p` and may mutate it at any point in the future.
    // Thus, the caller must not access `p` or any of its subobjects after DoPush() returns.
    void DoPush(string name, Override p) {
#if DEBUG
      Condition.Requires(name, nameof(name)).IsNotNullOrWhiteSpace();
      Condition.Requires(p, nameof(p)).IsNotNull();
#endif
      if (p.Fields.Count > 0) {
        p.Compact();
        _sink.Push(name, p, p.Timestamp ?? DateTime.UtcNow);
      }
    }

    // Copies tags and fields (but not the name) from `cols` to `p`.
    static void Extract<TColumns>(TColumns cols, PartialPoint point) {
      MemberExtractor<TColumns>.Instance.Extract(
          cols,
          point,
          (object payload, int idx, string tag) => {
            var p = (PartialPoint)payload;
            p.Tags.Add(new Indexed<string>(idx, tag));
          },
          (object payload, int idx, Field field) => {
            var p = (PartialPoint)payload;
            p.Fields.Add(new Indexed<Field>(idx, field));
          });
    }
  }

  class Override : PartialPoint {
    // If null, doesn't override timestamp.
    // DateTime.MinValue is special. Such values get replaced with DateTime.UtcNow in Facade.Push().
    public DateTime? Timestamp;

    public void MergeFrom(Override other) => MergeFrom(other?.Timestamp, other);

    public void MergeFrom(DateTime? t, PartialPoint p) {
      base.MergeFrom(p);
      Timestamp = t ?? Timestamp;
    }
  }

  class SingleThreadedOverrides {
    readonly LinkedList<Override> _data = new LinkedList<Override>();

    class Deleter : IDisposable {
      readonly int _thread;
      readonly SingleThreadedOverrides _outer;
      readonly LinkedListNode<Override> _data;

      public Deleter(SingleThreadedOverrides outer, LinkedListNode<Override> data) {
#if DEBUG
        Condition.Requires(outer, nameof(outer)).IsNotNull();
        Condition.Requires(data, nameof(data)).IsNotNull();
#endif
        _thread = Thread.CurrentThread.ManagedThreadId;
        _outer = outer;
        _data = data;
      }

      public void Dispose() {
        Condition.Requires(Thread.CurrentThread.ManagedThreadId, nameof(Thread.CurrentThread.ManagedThreadId))
            .IsEqualTo(_thread, "Override.Dispose() must be called from the same thread that has created it");
        if (_data.List != null) _outer._data.Remove(_data);
      }
    }

    // The caller must not mutate `p` or any of its subobjects after Add() returns.
    public IDisposable Add(Override p) => new Deleter(this, _data.AddLast(p));

    // It's OK to mutate the result. It doesn't alias anything.
    public void MergeTo(Override x) {
      foreach (Override data in _data) x.MergeFrom(data);
    }
  }

  class MultiThreadedOverrides {
    readonly object _monitor = new object();
    readonly LinkedList<Override> _data = new LinkedList<Override>();
    volatile Override _aggregate = new Override();

    class Deleter : IDisposable {
      readonly MultiThreadedOverrides _outer;
      readonly LinkedListNode<Override> _data;

      public Deleter(MultiThreadedOverrides outer, LinkedListNode<Override> data) {
        Condition.Requires(outer, nameof(outer)).IsNotNull();
        Condition.Requires(data, nameof(data)).IsNotNull();
        _outer = outer;
        _data = data;
      }

      public void Dispose() {
        lock (_outer._monitor) {
          if (_data.List != null) {
            _outer._data.Remove(_data);
            _outer.Update();
          }
        }
      }
    }

    // The caller must not mutate `p` or any of its subobjects after Add() returns.
    public IDisposable Add(Override p) {
      LinkedListNode<Override> node;
      lock (_monitor) {
        node = _data.AddLast(p);
        Update();
      }
      return new Deleter(this, node);
    }

    public void MergeTo(Override x) {
      x.MergeFrom(_aggregate);
    }

    void Update() {
      Condition.Requires(Monitor.IsEntered(_monitor)).IsTrue();
      var aggregate = new Override();
      foreach (Override x in _data) aggregate.MergeFrom(x);
      _aggregate = aggregate;
    }
  }
}
