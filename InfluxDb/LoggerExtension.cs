using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InfluxDb
{
    static class LoggerExtension
    {
        static readonly object _monitor = new object();
        static readonly Dictionary<Tuple<Reference<Logger>, string>, DateTime> _lastLogTime
            = new Dictionary<Tuple<Reference<Logger>, string>, DateTime>();

        // {log, message} is the key.
        public static void LogEvery(
            this Logger log, TimeSpan period, LogLevel level, string message, params object[] args)
        {
            if (ShouldLog(log, message, period))
            {
                log.Log(level, message, args);
            }
        }

        static bool ShouldLog(Logger log, string message, TimeSpan period)
        {
            DateTime now = DateTime.UtcNow;
            var key = Tuple.Create(new Reference<Logger>(log), message);
            lock (_monitor)
            {
                DateTime last;
                if (_lastLogTime.TryGetValue(key, out last) && now - last < period) return false;
                _lastLogTime[key] = now;
                return true;
            }
        }
    }
}
