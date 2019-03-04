// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core
{
    using System;

    public class SendFailureDetails
    {
        public SendFailureDetails(FailureKind failureKind, Exception rawException)
        {
            this.FailureKind = failureKind;
            this.RawException = rawException;
        }

        public FailureKind FailureKind { get; }

        public Exception RawException { get; }
    }
}
