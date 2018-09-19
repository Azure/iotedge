// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Util
{
    using System;
    using System.Threading.Tasks;
    using System.Timers;
    using Microsoft.Extensions.Logging;

    public class ResettableTimer : IDisposable
    {
        readonly Func<Task> callback;
        readonly TimeSpan period;
        readonly object lockobj = new object();
        readonly ILogger logger;
        Option<Timer> timer;

        public ResettableTimer(Func<Task> callback, TimeSpan period, ILogger logger)
            : this(callback, period, logger, true)
        {
        }

        public ResettableTimer(Func<Task> callback, TimeSpan period, ILogger logger, bool enable)
        {
            this.timer = Option.None<Timer>();
            this.period = period;
            this.callback = Preconditions.CheckNotNull(callback);
            this.logger = logger;
            this.Enable();
        }

        public void Start()
        {
            lock (this.lockobj)
            {
                this.timer.ForEach(t => t.Start());
            }
        }

        public void Reset()
        {
            lock (this.lockobj)
            {
                this.timer.ForEach(
                    t =>
                    {
                        t.Stop();
                        t.Start();
                    });
            }
        }

        public void Disable()
        {
            lock (this.lockobj)
            {
                this.timer.ForEach(t => t.Dispose());
                this.timer = Option.None<Timer>();
            }
        }

        public void Enable()
        {
            lock (this.lockobj)
            {
                if (!this.timer.HasValue)
                {
                    this.timer = Option.Some(this.CreateTimer());
                }
            }
        }

        Timer CreateTimer()
        {
            var instance = new Timer(this.period.TotalMilliseconds);
            instance.Elapsed += this.TimerOnElapsed;
            return instance;
        }

        async void TimerOnElapsed(object sender, ElapsedEventArgs e)
        {
            try
            {
                await this.callback.Invoke();
            }
            catch (Exception exception)
            {
                this.logger?.LogWarning($"Error in timer callback - {exception}");
            }
        }

        public void Dispose() => this.Disable();
    }
}
