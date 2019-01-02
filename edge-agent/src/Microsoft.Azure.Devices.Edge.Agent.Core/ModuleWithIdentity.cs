// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    using System.Collections.Generic;
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
            int hashCode = 1536872568;
            hashCode = hashCode * -1521134295 + EqualityComparer<IModule>.Default.GetHashCode(this.Module);
            hashCode = hashCode * -1521134295 + EqualityComparer<IModuleIdentity>.Default.GetHashCode(this.ModuleIdentity);
            return hashCode;
        }
    }
}
