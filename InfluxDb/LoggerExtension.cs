using Conditions;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InfluxDb
{
    public static class LoggerExtension
    {
        static readonly object _monitor = new object();
        static readonly HashSet<Tuple<Reference<Logger>, string>> _set =
            new HashSet<Tuple<Reference<Logger>, string>>();
        // DateTime is in non-decreasing order. It's the log time.
        // Invariant: each combination of logger (Item1) and message (Item2) is present at most once.
        // Invariant: there is trivial one-to-one correspondence between the elements of _set and _queue.
        static readonly Queue<Tuple<Reference<Logger>, string, DateTime>> _queue =
            new Queue<Tuple<Reference<Logger>, string, DateTime>>();

        // {log, message} is the key.
        // Does nothing if a log messages with the same key has been written less than `period` ago.
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
            DateTime cutoff = now - period;
            var key = Tuple.Create(new Reference<Logger>(log), message);
            lock (_monitor)
            {
                while (_queue.Count > 0 && _queue.Peek().Item3 <= cutoff)
                {
                    var top = _queue.Dequeue();
                    Condition.Requires(_set.Remove(Tuple.Create(top.Item1, top.Item2))).IsTrue();
                }
                if (!_set.Add(key)) return false;
                _queue.Enqueue(Tuple.Create(key.Item1, key.Item2, now));
                return true;
            }
        }
    }
}
