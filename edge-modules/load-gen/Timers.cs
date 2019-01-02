// Copyright (c) Microsoft. All rights reserved.
namespace LoadGen
{
    using System;
    using System.Collections.Generic;
    using System.Timers;

    public class Timers : IDisposable
    {
        private List<TimerTask> timerTasks = new List<TimerTask>();

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    foreach (TimerTask task in this.timerTasks)
                    {
                        task.Timer.Dispose();
                    }
                }

                disposedValue = true;
            }
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
        }
        #endregion

        public void Add(TimeSpan interval, double jitterFactor, Action callback)
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
    }

    class TimerTask
    {
        public Action Callback { get; }
        public Timer Timer { get; }
        public bool Quit { get; set; }

        public TimerTask(TimeSpan interval, double jitterFactor, Action callback)
        {
            this.Callback = callback;
            this.Quit = false;

            this.Timer = new Timer(interval.TotalMilliseconds);
            this.Timer.AutoReset = false;
            this.Timer.Enabled = false;

            var random = new Random();
            this.Timer.Elapsed += (source, args) =>
            {
                // invoke callback
                this.Callback();

                // schedule next callback adding jitter if necessary
                if (this.Quit == false)
                {
                    this.Timer.Enabled = false;
                    this.Timer.Interval = TimerTask.ApplyJitter(random, interval, jitterFactor).TotalMilliseconds;
                    this.Timer.Enabled = true;
                }
            };
        }

        static TimeSpan ApplyJitter(Random random, TimeSpan interval, double jitterFactor)
        {
            double sign = random.NextDouble() > 0.5 ? 1.0 : -1.0;
            double variance = interval.TotalMilliseconds * jitterFactor;
            double jitter = random.NextDouble() * variance * sign;

            return interval + TimeSpan.FromMilliseconds(jitter);
        }
    }
}
