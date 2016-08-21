using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace InfluxDb
{
    // Scheduler runs all actions asynchronously and serially. Different actions may
    // run in different threads but they won't run concurrently.
    // Actions can be scheduled in the future. The execution order is what you
    // would expect: stable sorting by the scheduled time.
    public class Scheduler : IDisposable
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();

        readonly ScheduledQueue<Action> _actions = new ScheduledQueue<Action>();
        readonly CancellationTokenSource _dispose = new CancellationTokenSource();
        readonly Task _loop;

        // Launches a background task. Call Dispose() to stop it.
        public Scheduler()
        {
            _loop = Task.Run(ActionLoop);
        }

        // Schedules the specified action to run at the specified time.
        //
        // When the returned Func is called, it cancels the action if it hasn't run yet
        // and returns true. If the action has already run or is about to run, it does
        // nothing and returns false. It's OK to call it multiple times (all consequent
        // calls return false).
        public Func<bool> Schedule(DateTime when, Action action)
        {
            return _actions.Push(action, when);
        }

        // Schedules the specified action to run ASAP.
        public Func<bool> Schedule(Action action)
        {
            return Schedule(DateTime.UtcNow, action);
        }

        // Returns true if there are ready actions in the queue. The action that may currently be
        // running doesn't count.
        public bool HasReady()
        {
            return _actions.HasReady();
        }

        // Blocks until the background thread is stopped.
        public void Dispose()
        {
            _log.Info("Disposing of InfluxDb.Scheduler");
            _dispose.Cancel();
            try { _loop.Wait(); } catch { }
            _log.Info("InfluxDb.Scheduler successfully disposed of");
        }

        async Task ActionLoop()
        {
            while (true)
            {
                ReadyMessage<Action> action = await _actions.Next(_dispose.Token);
                try
                {
                    action.Message.Invoke();
                }
                catch (Exception e)
                {
                    _log.Warn(e, "Ignoring exception from the user action");
                }
            }
        }
    }
}
