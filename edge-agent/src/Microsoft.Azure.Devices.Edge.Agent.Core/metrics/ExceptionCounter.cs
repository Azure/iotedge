// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core.Metrics
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.ExceptionServices;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Metrics;

    public class ExceptionCounter : IDisposable
    {
        readonly IMetricsCounter exceptions;
        readonly Dictionary<Type, string> recognizedExceptions;
        readonly HashSet<Type> ignoredExceptions;

        public ExceptionCounter(Dictionary<Type, string> recognizedExceptions, HashSet<Type> ignoredExceptions, IMetricsProvider metricsProvider)
        {
            this.recognizedExceptions = Preconditions.CheckNotNull(recognizedExceptions, nameof(recognizedExceptions));
            this.ignoredExceptions = Preconditions.CheckNotNull(ignoredExceptions, nameof(ignoredExceptions));
            Preconditions.CheckNotNull(metricsProvider, nameof(metricsProvider));
            this.exceptions = Preconditions.CheckNotNull(metricsProvider.CreateCounter(
                "exceptions_total",
                "The number of exceptions thrown of the given type",
                new List<string> { "exception_name" }));

            AppDomain.CurrentDomain.FirstChanceException += this.OnException;
        }

        public void Dispose()
        {
            AppDomain.CurrentDomain.FirstChanceException -= this.OnException;
        }

        void OnException(object source, FirstChanceExceptionEventArgs e)
        {
            if (this.ignoredExceptions.Contains(e.Exception.GetType()))
            {
                return;
            }

            if (this.recognizedExceptions.TryGetValue(e.Exception.GetType(), out string name))
            {
                this.exceptions.Increment(1, new string[] { name });
            }
            else
            {
                this.exceptions.Increment(1, new string[] { "other" });
            }
        }
    }
}
