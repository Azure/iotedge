// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Metrics.NullMetrics
{
    using System;

    public class NullDisposable : IDisposable
    {
        NullDisposable() { }

        public static IDisposable Instance = new NullDisposable();

        public void Dispose()
        {
        }
    }
}
