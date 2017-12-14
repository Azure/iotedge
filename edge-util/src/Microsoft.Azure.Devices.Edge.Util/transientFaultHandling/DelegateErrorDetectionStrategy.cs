// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Util.TransientFaultHandling
{
    using System;

    /// <summary>
    /// An error detection strategy that delegates the detection to a lambda.
    /// </summary>
    public class DelegateErrorDetectionStrategy : ITransientErrorDetectionStrategy
    {
        readonly Func<Exception, bool> underlying;

        public DelegateErrorDetectionStrategy(Func<Exception, bool> isTransient)
        {
            this.underlying = Preconditions.CheckNotNull(isTransient);
        }

        public bool IsTransient(Exception ex) => this.underlying(ex);
    }
}
