// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core.Metrics
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.ExceptionServices;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util.Metrics;

    public class ExceptionCounter : IDisposable
    {
        private readonly IMetricsCounter exceptions = Util.Metrics.Metrics.Instance.CreateCounter(
            "exceptions_total",
            "The number of exeptions thrown of the given type",
            new List<string> { "exception_name" });

        private Dictionary<Type, string> recognizedExceptions;

        private HashSet<Type> ignoredExeptions;

        public ExceptionCounter(Dictionary<Type, string> recognizedExceptions, HashSet<Type> ignoredExeptions)
        {
            this.recognizedExceptions = recognizedExceptions;
            this.ignoredExeptions = ignoredExeptions;

            AppDomain.CurrentDomain.FirstChanceException += this.OnException;
        }

        public void Dispose()
        {
            AppDomain.CurrentDomain.FirstChanceException -= this.OnException;
        }

        private void OnException(object source, FirstChanceExceptionEventArgs e)
        {
            if (this.ignoredExeptions.Contains(e.Exception.GetType()))
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
