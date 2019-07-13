﻿using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InfluxDb {
  // https://docs.influxdata.com/influxdb/v0.13/write_protocols/line/
  static class Serializer {
    static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    static readonly char[] KeySpecialChars = new char[] { '\\', ' ', ',', '=' };

    public static readonly char[] FieldSpecialChars = new char[] { '\\', '"' };

    static readonly Logger _log = LogManager.GetCurrentClassLogger();

    public static string Serialize(IEnumerable<Point> points) {
      var valid = new StringBuilder();
      var invalid = new StringBuilder();
      foreach (Point p in points) {
        int checkpoint = valid.Length;
        if (valid.Length > 0) valid.Append('\n');
        if (!WritePoint(p, valid)) {
          invalid.Append("\n  ");
          int start = checkpoint == 0 ? 0 : checkpoint + 1;
          invalid.Append(valid.ToString(start, valid.Length - start));
          valid.Length = checkpoint;
        }
      }
      if (invalid.Length > 0) {
        _log.Error("Dropping point(s) that would have invalid serialized representation:{0}", invalid);
      }
      return valid.ToString();
    }

    static bool WritePoint(Point p, StringBuilder sb) {
      bool res = true;
      res &= WriteKey(p.Key.Name, sb);
      foreach (var kv in Named(NameTable.Tags.Array, p.Key.Tags).OrderBy(kv => kv.Key)) {
        sb.Append(',');
        res &= WriteKey(kv.Key, sb);
        sb.Append('=');
        res &= WriteTag(kv.Value, sb);
      }

      sb.Append(' ');
      bool first = true;
      foreach (var kv in Named(NameTable.Fields.Array, p.Value.Fields)) {
        if (first) first = false;
        else sb.Append(',');

        res &= WriteKey(kv.Key, sb);
        sb.Append('=');
        res &= kv.Value.SerializeTo(sb);
      }

      sb.Append(' ');
      WriteTimestamp(p.Value.Timestamp, sb);
      return res;
    }

    static bool WriteTag(string tag, StringBuilder sb) => WriteKey(tag, sb);

    static void WriteTimestamp(DateTime t, StringBuilder sb) {
      // This code works like a static assert.
      // It won't compile if TimeSpan.TicksPerSecond isn't equal to 10000000.
      const uint x = TimeSpan.TicksPerSecond == 10000000 ? 100 : -1;
      long ns = x * (t - Epoch).Ticks;
      sb.Append(ns);
    }

    static bool WriteKey(string key, StringBuilder sb) {
      if (key == null) {
        sb.Append("null");
        return false;
      } else {
        Strings.Escape(key, '\\', KeySpecialChars, sb);
        return true;
      }
    }

    static IEnumerable<KeyValuePair<string, T>> Named<T>(string[] names, List<T> values) {
      return values.Where(v => v != null).Select((T v, int i) => new KeyValuePair<string, T>(names[i], v));
    }
  }
}
