using Conditions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace InfluxDb
{
    class ReadyMessage<T>
    {
        // The payload.
        public T Message;
        // If true, there are no ready messages behind this one.
        // In other words, this is the last ready message.
        public bool Last;
    }

    // A queue of items with their expected processing time.
    // Thread-safe for multiple producers and single consumer.
    class ScheduledQueue<TValue>
    {
        readonly PriorityQueue<DateTime, TValue> _data = new PriorityQueue<DateTime, TValue>();
        readonly object _monitor = new object();
        // If not null, there may be an active Next() call that is waiting for the next value.
        Task _next = null;

        // Adds an element to the queue. It'll be ready for processing at the specified time.
        public Func<bool> Push(TValue value, DateTime when)
        {
            Func<bool> cancel;
            lock (_monitor)
            {
                cancel = _data.Push(when, value);
                if (_next != null)
                {
                    Task next = _next;
                    _next = null;
                    Task.Run(() => next.RunSynchronously());
                }
            }
            return () => { lock (_monitor) return cancel(); };
        }

        // Returns the next ready message when it becomes ready.
        // There must be at most one active task produced by Next().
        // In other words, it's illegal to call Next() before the task produced
        // by the previous call to Next() finishes.
        public async Task<ReadyMessage<TValue>> Next(CancellationToken cancel)
        {
            while (true)
            {
                // TimeSpan.FromMilliseconds(-1) is infinity for Task.Delay().
                TimeSpan delay = TimeSpan.FromMilliseconds(-1);
                Task next = null;
                lock (_monitor)
                {
                    if (_next != null)
                    {
                        // If we don't run the task, it'll never get deleted.
                        _next.RunSynchronously();
                        _next = null;
                    }
                    if (_data.Any())
                    {
                        DateTime now = DateTime.UtcNow;
                        if (_data.Front().Key <= now)
                        {
                            return new ReadyMessage<TValue>()
                            {
                                Message = _data.Pop().Value,
                                Last = !_data.Any() || _data.Front().Key > now
                            };
                        }
                        delay = _data.Front().Key - now;
                        // Task.Delay() can't handle values above TimeSpan.FromMilliseconds(Int32.MaxValue).
                        if (delay > TimeSpan.FromMilliseconds(Int32.MaxValue))
                        {
                            delay = TimeSpan.FromMilliseconds(Int32.MaxValue);
                        }
                    }
                    _next = new Task(delegate { }, cancel);
                    next = _next;
                }
                using (var c = new CancellationTokenSource())
                {
                    try
                    {
                        // Task.WhenAny() returns Task<Task>, hence the double await.
                        // Will throw if cancellation is requested through `cancel`.
                        await await Task.WhenAny(Task.Delay(delay, c.Token), next);
                    }
                    finally
                    {
                        // The delay task won't be garbage collected until it completes
                        // (which can be "never"), so we cancel it manually.
                        c.Cancel();
                    }
                }
            }
        }

        public bool HasReady()
        {
            lock (_monitor)
            {
                return _data.Any() && _data.Front().Key <= DateTime.UtcNow;
            }
        }
    }
}
