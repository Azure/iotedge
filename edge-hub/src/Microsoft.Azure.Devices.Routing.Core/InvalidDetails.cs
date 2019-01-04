// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core
{
    public class InvalidDetails<T>
    {
        public InvalidDetails(T item, FailureKind failureKind)
        {
            this.Item = item;
            this.FailureKind = failureKind;
        }

        public T Item { get; }

        public FailureKind FailureKind { get; }
    }
}
