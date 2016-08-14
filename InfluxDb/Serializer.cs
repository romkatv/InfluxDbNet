using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InfluxDb
{
    static class Serializer
    {
        static readonly char[] KeySpecialChars = new char[] { '\\', ' ', '.', '=' };

        public static string Serialize(List<Point> points)
        {
            return "";
        }

        static string EscapeKey(string key)
        {
            return Escape(key, KeySpecialChars);
        }

        static string Escape(string s, char[] chars)
        {
            int next = s.IndexOfAny(chars);
            if (next < 0) return s;  // this is an optimization
            int start = 0;
            var res = new StringBuilder(2 * s.Length - next);
            while (next >= 0)
            {
                res.Append(s, start, next - start);
                res.Append('\\');
                res.Append(s[next]);
                start = next + 1;
                next = s.IndexOfAny(chars, start);
            }
            res.Append(s, start, s.Length - start);
            return res.ToString();
        }
    }
}
