// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Routing.Core.Util
{
    using System;

    using Microsoft.Azure.Devices.Edge.Util.TransientFaultHandling;

    public class ErrorDetectionStrategy : ITransientErrorDetectionStrategy
    {
        readonly Func<Exception, bool> underlying;

        public ErrorDetectionStrategy(Func<Exception, bool> isTransient)
        {
            this.underlying = Preconditions.CheckNotNull(isTransient);
        }

        public bool IsTransient(Exception ex) => this.underlying(ex);
    }
}
