using Conditions;
using NLog;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace InfluxDb
{
    // Supports running an action periodically.
    class PeriodicAction : IDisposable
    {
        static readonly Logger _log = LogManager.GetCurrentClassLogger();

        readonly object _monitor = new object();
        readonly Scheduler _scheduler;
        readonly Action _work;
        readonly TimeSpan _period;
        // These fields are protected by _monitor.
        DateTime _next;
        Func<bool> _cancel;
        bool _disposed = false;

        // Runs the action after the specified delay and then periodically. Does not block. Action runs are serialized.
        // Given three consecutive action runs a1, a2, and a3, it's guaranteed that start_time(a3) - end_time(a1) >= period.
        // If actions run fast enough, there is no slippage (N-th action starts soon after delay + N * period).
        // If slippage occurs due to a slow run of one of the actions, it's permanent (all future action runs will be
        // delayed).
        //
        // The action runs on the provided scheduler.
        //
        // To stop running the action, call Dispose().
        public PeriodicAction(Scheduler scheduler, TimeSpan delay, TimeSpan period, Action work)
        {
            Condition.Requires(scheduler, "scheduler").IsNotNull();
            Condition.Requires(work, "work").IsNotNull();
            Condition.Requires(delay, "delay").IsGreaterOrEqual(TimeSpan.Zero);
            Condition.Requires(period, "period").IsGreaterOrEqual(TimeSpan.Zero);
            _scheduler = scheduler;
            _work = work;
            _period = period;
            lock (_monitor)
            {
                _next = DateTime.UtcNow + delay;
                _cancel = _scheduler.Schedule(_next, DoRun);
            }
        }

        // Guarantees that a fresh run will happen ASAP.
        // If the action is currently running, another one
        // will start immediately after it finishes.
        //
        // It's allowed to call RunSoon() from the action.
        public void RunSoon()
        {
            lock (_monitor)
            {
                if (_disposed) throw new ObjectDisposedException("PeriodicAction");
                if (_cancel.Invoke())
                {
                    _next = DateTime.UtcNow;
                    _cancel = _scheduler.Schedule(_next, DoRun);
                }
                else
                {
                    _next = DateTime.UtcNow - _period;
                }
            }
        }

        void DoRun()
        {
            try { _work.Invoke(); }
            catch (Exception e) { _log.Warn(e, "Ignoring exception from periodic action"); }
            DateTime end = DateTime.UtcNow;
            lock (_monitor)
            {
                if (_disposed) return;
                _next = Max(_next + _period, end);
                _cancel = _scheduler.Schedule(_next, DoRun);
            }
        }

        // Doesn't block, even if the action is currently running in another thread.
        public void Dispose()
        {
            lock (_monitor)
            {
                _disposed = true;
                // Best effort. If _cancel() returns false, the action is currently
                // running. Don't wait for it to finish.
                _cancel.Invoke();
            }
        }

        static DateTime Max(DateTime a, DateTime b)
        {
            return a > b ? a : b;
        }
    }
}
