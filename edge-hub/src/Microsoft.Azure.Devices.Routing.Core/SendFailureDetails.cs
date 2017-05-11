// ---------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ---------------------------------------------------------------

namespace Microsoft.Azure.Devices.Routing.Core
{
    using System;

    public class SendFailureDetails
    {
        public FailureKind FailureKind { get; }

        public Exception RawException { get; }

        public SendFailureDetails(FailureKind failureKind, Exception rawException)
        {
            this.FailureKind = failureKind;
            this.RawException = rawException;
        }
    }
}
