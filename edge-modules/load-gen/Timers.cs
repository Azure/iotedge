// Copyright (c) Microsoft. All rights reserved.
namespace LoadGen
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using System.Timers;

    public class Timers : IDisposable
    {
        readonly List<TimerTask> timerTasks = new List<TimerTask>();

        bool disposedValue = false; // To detect redundant calls

        public void Add(TimeSpan interval, double jitterFactor, Func<Task> callback)
        {
            this.timerTasks.Add(new TimerTask(interval, jitterFactor, callback));
        }

        public void Start()
        {
            foreach (TimerTask task in this.timerTasks)
            {
                task.Timer.Start();
            }
        }

        public void Stop()
        {
            foreach (TimerTask task in this.timerTasks)
            {
                task.Timer.Stop();
                task.Quit = true;
            }
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            this.Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposedValue)
            {
                if (disposing)
                {
                    foreach (TimerTask task in this.timerTasks)
                    {
                        task.Timer.Dispose();
                    }
                }

                this.disposedValue = true;
            }
        }
    }

    class TimerTask
    {
        public TimerTask(TimeSpan interval, double jitterFactor, Func<Task> callback)
        {
            this.Callback = callback;
            this.Quit = false;

            this.Timer = new Timer(interval.TotalMilliseconds);
            this.Timer.AutoReset = false;
            this.Timer.Enabled = false;

            var random = new Random();
            this.Timer.Elapsed += async (source, args) =>
            {
                // invoke callback
                await this.Callback();

                // schedule next callback adding jitter if necessary
                if (this.Quit == false)
                {
                    this.Timer.Enabled = false;
                    this.Timer.Interval = ApplyJitter(random, interval, jitterFactor).TotalMilliseconds;
                    this.Timer.Enabled = true;
                }
            };
        }

        public Func<Task> Callback { get; }

        public Timer Timer { get; }

        public bool Quit { get; set; }

        static TimeSpan ApplyJitter(Random random, TimeSpan interval, double jitterFactor)
        {
            double sign = random.NextDouble() > 0.5 ? 1.0 : -1.0;
            double variance = interval.TotalMilliseconds * jitterFactor;
            double jitter = random.NextDouble() * variance * sign;

            return interval + TimeSpan.FromMilliseconds(jitter);
        }
    }
}
