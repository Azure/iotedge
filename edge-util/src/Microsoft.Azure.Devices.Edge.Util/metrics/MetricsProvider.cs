// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.metrics
{
    using System;
    using System.Collections;
    using System.Collections.Generic;

    public static class Metrics
    {
        IMetricsCounter Counter { get; } = new NullMetricsCounter();

        IMetricsMeter Meter { get; } = new NullMetricsMeter();

        IMetricsTimer Timer { get; } = new NullMetricsTimer();

        IMetricsHistogram Histogram { get; } = new NullMetricsHistogram();
    }

    public interface IMetricsProvider
    {
        ICounter GetCounter(string name);

        ICounter GetCounter(string name);
    }

    public interface ICounter
    {
        void Increment(long amount);
        void Decrement(long amount);
        void Increment(long amount, IDictionary<string, string> tags);
        void Decrement(long amount, IDictionary<string, string> tags);
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
