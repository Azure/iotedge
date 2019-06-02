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

    public class NullMetricsProvider : IMetricsProvider
    {
        public ICounter CreateCounter(string name, Dictionary<string, string> tags) => new NullCounter();
    }

    public class NullCounter : ICounter
    {
        public void Increment(long amount) { }

        public void Decrement(long amount)
        {
        }

        public void Increment(long amount, Dictionary<string, string> tags)
        {
        }

        public void Decrement(long amount, Dictionary<string, string> tags)
        { }
    }

    public interface IMetricsProvider
    {
        ICounter CreateCounter(string name, Dictionary<string, string> tags);
    }

    public interface ICounter
    {
        void Increment(long amount);
        void Decrement(long amount);
        void Increment(long amount, Dictionary<string, string> tags);
        void Decrement(long amount, Dictionary<string, string> tags);
    }

    public interface IGauge
    {
        void Set(long value);
        void Set(long value, IDictionary<string, string> tags);
    }

    public interface IMeter
    {
        void Mark();
        void Mark(IDictionary<string, string> tags);
    }

    public interface IMetricsTimer
    {
        IDisposable GetTimer();
        IDisposable GetTimer(IDictionary<string, string> tags);
    }

    public interface IMetricsHistogram
    {
        void Update(long value);
        void Update(long value, IDictionary<string, string> tags);
    }
}
