// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Routing.Core
{
    public class InvalidDetails<T>
    {
        public InvalidDetails(T item, FailureKind failureKind)
        {
            this.Item = item;
            this.FailureKind = failureKind;
        }

        public FailureKind FailureKind { get; }

        public T Item { get; }
    }
}
