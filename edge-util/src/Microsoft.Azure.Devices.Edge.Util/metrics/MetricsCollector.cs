// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Metrics
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using Microsoft.Azure.Devices.Edge.Util.Metrics.AppMetrics;

    public static class Metrics
    {
        static IMetricsProvider instance = new NullMetricsProvider();

        public static IMetricsProvider Instance => instance;

        public static void InitPrometheusMetrics(string url)
        {
            instance = MetricsProvider.CreatePrometheusExporter(url);
        }
    }

    

    

    

    

    

    

    

    public interface IMetricsCounter
    {
        void Increment(long amount);
        void Decrement(long amount);
        void Increment(long amount, Dictionary<string, string> tags);
        void Decrement(long amount, Dictionary<string, string> tags);
    }

    

    

    

    
}
