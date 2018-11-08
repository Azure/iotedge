// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Routing.Core.Checkpointers
{
    public class CheckpointerStatus
    {
        public CheckpointerStatus(string id, long offset, long proposed)
        {
            this.Id = id;
            this.Offset = offset;
            this.Proposed = proposed;
        }

        public string Id { get; }

        public long Offset { get; }

        public long Proposed { get; }
    }
}
