// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Metrics
{
    using System;
    using System.Collections.Generic;

    public class Metrics
    {
        public static IMetricsProvider Instance { get; }
    }

    public interface IMetricsProvider
    {
        ICounter CreateCounter(string name, IDictionary<string, string> tags);

        IGauge CreateGauge(string name, IDictionary<string, string> gauge);

        IMetricsTimer CreateTimer(string name);
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
