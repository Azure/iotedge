// Copyright (c) Microsoft. All rights reserved.
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
