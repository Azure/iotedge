// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    using Microsoft.Azure.Devices.Edge.Util;

    public class ModuleWithIdentity : IModuleWithIdentity
    {
        readonly IModule module;
        readonly IModuleIdentity moduleIdentity;

        public ModuleWithIdentity(IModule module, IModuleIdentity moduleIdentity)
        {
            this.module = Preconditions.CheckNotNull(module, nameof(module));
            this.moduleIdentity = Preconditions.CheckNotNull(moduleIdentity, nameof(moduleIdentity));
        }

        public IModule Module => this.module;

        public IModuleIdentity ModuleIdentity => this.moduleIdentity;

        public override bool Equals(object obj) => this.Equals(obj as ModuleWithIdentity);

        public virtual bool Equals(IModule other) => this.Equals(other as ModuleWithIdentity);

        public virtual bool Equals(IModuleWithIdentity other)
        {
            if (ReferenceEquals(null, other))
                return false;
            if (ReferenceEquals(this, other))
                return true;
            return this.module.Equals(other.Module) && this.moduleIdentity.Equals(other.ModuleIdentity);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = base.GetHashCode();
                hashCode = (hashCode * 397) ^ this.module.GetHashCode();
                hashCode = (hashCode * 397) ^ this.moduleIdentity.GetHashCode();
                return hashCode;
            }
        }
    }
}
