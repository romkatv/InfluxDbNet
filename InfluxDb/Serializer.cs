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

        public static string Serialize(List<Point> points)
        {
            var sb = new StringBuilder();
            for (int i = 0; i != points.Count; ++i)
            {
                if (i != 0) sb.Append('\n');
                WritePoint(points[i], sb);
            }
            return sb.ToString();
        }

        static void WritePoint(Point p, StringBuilder sb)
        {
            WriteKey(p.Name, sb);
            foreach (var kv in p.Tags)
            {
                sb.Append(',');
                WriteKey(kv.Key, sb);
                sb.Append('=');
                WriteTag(kv.Value, sb);
            }

            sb.Append(' ');
            bool first = true;
            foreach (var kv in p.Fields)
            {
                if (first) first = false;
                else sb.Append(',');

                WriteKey(kv.Key, sb);
                WriteField(kv.Value, sb);
            }

            sb.Append(' ');
            WriteTimestamp(p.Timestamp, sb);
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
                    Escape(x, FieldSpecialChars, sb);
                    sb.Append('"');
                    return null;
                }
            );
        }

        static void WriteTimestamp(DateTime t, StringBuilder sb)
        {
            long micros = (long)(1000 * (t - Epoch).TotalMilliseconds);
            sb.Append(micros);
        }

        static void WriteKey(string key, StringBuilder sb)
        {
            Escape(key, KeySpecialChars, sb);
        }

        static void Escape(string s, char[] chars, StringBuilder sb)
        {
            int start = 0;
            while (true)
            {
                int next = s.IndexOfAny(chars);
                if (next < 0) break;
                sb.Append(s, start, next - start);
                sb.Append('\\');
                sb.Append(s[next]);
                start = next + 1;
            }
            sb.Append(s, start, s.Length - start);
        }
    }
}
