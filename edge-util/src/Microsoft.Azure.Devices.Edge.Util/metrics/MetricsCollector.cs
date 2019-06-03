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

        public IMetricsGauge CreateGauge(string name, Dictionary<string, string> defaultTags) => new NullGauge();

        public IMetricsHistogram CreateHistogram(string name, Dictionary<string, string> defaultTags)
            => new NullMetricsHistogram();

        public IMetricsMeter CreateMeter(string name, Dictionary<string, string> defaultTags)
            => new NullMeter();

        public IMetricsTimer CreateTimer(string name, Dictionary<string, string> defaultTags)
            => new NullMetricsTimer();
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

    public class NullGauge : IMetricsGauge
    {
        public void Set(long value)
        {
        }

        public void Set(long value, Dictionary<string, string> tags)
        {
        }
    }

    public class NullMeter : IMetricsMeter
    {
        public void Mark()
        {
        }

        public void Mark(Dictionary<string, string> tags)
        {
        }
    }

    public class NullMetricsTimer : IMetricsTimer
    {
        public IDisposable GetTimer() => NullDisposable.Instance;

        public IDisposable GetTimer(Dictionary<string, string> tags) => NullDisposable.Instance;
    }

    public class NullMetricsHistogram : IMetricsHistogram
    {
        public void Update(long value)
        {
        }

        public void Update(long value, Dictionary<string, string> tags)
        {
        }
    }

    public class NullDisposable : IDisposable
    {
        NullDisposable() { }

        public static IDisposable Instance = new NullDisposable();

        public void Dispose()
        {
        }
    }

    public interface IMetricsProvider
    {
        ICounter CreateCounter(string name, Dictionary<string, string> tags);

        IMetricsGauge CreateGauge(string name, Dictionary<string, string> defaultTags);

        IMetricsMeter CreateMeter(string name, Dictionary<string, string> defaultTags);

        IMetricsTimer CreateTimer(string name, Dictionary<string, string> defaultTags);

        IMetricsHistogram CreateHistogram(string name, Dictionary<string, string> defaultTags);
    }

    public interface ICounter
    {
        void Increment(long amount);
        void Decrement(long amount);
        void Increment(long amount, Dictionary<string, string> tags);
        void Decrement(long amount, Dictionary<string, string> tags);
    }

    public interface IMetricsGauge
    {
        void Set(long value);
        void Set(long value, Dictionary<string, string> tags);
    }

    public interface IMetricsMeter
    {
        void Mark();
        void Mark(Dictionary<string, string> tags);
    }

    public interface IMetricsTimer
    {
        IDisposable GetTimer();
        IDisposable GetTimer(Dictionary<string, string> tags);
    }

    public interface IMetricsHistogram
    {
        void Update(long value);
        void Update(long value, Dictionary<string, string> tags);
    }
}
