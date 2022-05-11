// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Metrics.NullMetrics
{
    using System;

    public class NullDisposable : IDisposable
    {
        public static IDisposable Instance = new NullDisposable();

        NullDisposable()
        {
        }

        public void Dispose()
        {
        }
    }
}
