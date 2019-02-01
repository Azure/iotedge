// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Edgelet.Models
{
    using Microsoft.Azure.Devices.Edge.Util;

    public class Identity
    {
        public Identity(string moduleId, string generationId, string managedBy)
        {
            this.ModuleId = Preconditions.CheckNonWhiteSpace(moduleId, nameof(moduleId));
            this.GenerationId = Preconditions.CheckNonWhiteSpace(generationId, nameof(generationId));
            this.ManagedBy = Preconditions.CheckNotNull(managedBy, nameof(managedBy));
        }

        public string ModuleId { get; }

        public string ManagedBy { get; }

        public string GenerationId { get; }
    }
}
