// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub
{
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Util;

    class ModuleIdentity : IModuleIdentity
    {
        public ModuleIdentity(string moduleId, string connectionString)
        {
            this.ConnectionString = Preconditions.CheckNotNull(connectionString, nameof(connectionString));
            this.Name = Preconditions.CheckNotNull(moduleId, nameof(moduleId));
        }

        public string Name { get; }

        public string ConnectionString { get; }

        public override bool Equals(object obj) => this.Equals(obj as ModuleIdentity);

        public bool Equals(IModuleIdentity other)
        {
            if (ReferenceEquals(null, other))
                return false;
            if (ReferenceEquals(this, other))
                return true;

            return string.Equals(this.Name, other.Name)
                && string.Equals(this.ConnectionString, other.ConnectionString);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((this.Name != null ? this.Name.GetHashCode() : 0) * 397) ^ (this.ConnectionString != null ? this.ConnectionString.GetHashCode() : 0);
            }
        }
    }
}
