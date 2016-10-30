using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InfluxDb
{
    // https://docs.influxdata.com/influxdb/v0.13/write_protocols/line/
    static class Serializer
    {
        static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        static readonly char[] KeySpecialChars = new char[] { '\\', ' ', ',', '=' };
        static readonly char[] FieldSpecialChars = new char[] { '\\', '"' };

        public static string Serialize(IEnumerable<Point> points)
        {
            var sb = new StringBuilder();
            bool first = true;
            foreach (Point p in points)
            {
                if (first) first = false;
                else sb.Append('\n');
                WritePoint(p, sb);
            }
            return sb.ToString();
        }

        static void WritePoint(Point p, StringBuilder sb)
        {
            WriteKey(p.Key.Name, sb);
            foreach (var kv in p.Key.Tags.OrderBy(kv => kv.Key))
            {
                sb.Append(',');
                WriteKey(kv.Key, sb);
                sb.Append('=');
                WriteTag(kv.Value, sb);
            }

            sb.Append(' ');
            bool first = true;
            foreach (var kv in p.Value.Fields)
            {
                if (first) first = false;
                else sb.Append(',');

                WriteKey(kv.Key, sb);
                sb.Append('=');
                WriteField(kv.Value, sb);
            }

            sb.Append(' ');
            WriteTimestamp(p.Value.Timestamp, sb);
        }

        static void WriteTag(string tag, StringBuilder sb)
        {
            WriteKey(tag, sb);
        }

        static void WriteField(Field f, StringBuilder sb)
        {
            f.Visit<object>
            (
                (long x) =>
                {
                    sb.Append(x);
                    sb.Append('i');
                    return null;
                },
                (double x) =>
                {
                    sb.AppendFormat("{0:R}", x);
                    return null;
                },
                (bool x) =>
                {
                    sb.Append(x ? "true" : "false");
                    return null;
                },
                (string x) =>
                {
                    sb.Append('"');
                    Strings.Escape(x, '\\', FieldSpecialChars, sb);
                    sb.Append('"');
                    return null;
                }
            );
        }

        static void WriteTimestamp(DateTime t, StringBuilder sb)
        {
            // This code works like a static assert.
            // It won't compile if TimeSpan.TicksPerSecond isn't equal to 10000000.
            const uint x = TimeSpan.TicksPerSecond == 10000000 ? 100 : -1;
            long ns = x * (t - Epoch).Ticks;
            sb.Append(ns);
        }

        static void WriteKey(string key, StringBuilder sb)
        {
            Strings.Escape(key, '\\', KeySpecialChars, sb);
        }
    }
}
