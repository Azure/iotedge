// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Util;
    using Newtonsoft.Json;

    public class KubernetesModule
    {
        [JsonProperty(PropertyName = "module")]
        public readonly IModule Module;

        [JsonProperty(PropertyName = "moduleIdentity")]
        public readonly KubernetesModuleIdentity ModuleIdentity;

        public KubernetesModule(IModule module, KubernetesModuleIdentity moduleIdentity)
        {
            this.Module = Preconditions.CheckNotNull(module, nameof(module));
            this.ModuleIdentity = Preconditions.CheckNotNull(moduleIdentity, nameof(moduleIdentity));
        }
        
        public virtual bool Equals(KubernetesModule other)
        {
            if (ReferenceEquals(null, other))
                return false;
            if (ReferenceEquals(this, other))
                return true;
            return this.Module.Equals(other.Module) && this.ModuleIdentity.Equals(other.ModuleIdentity);
        }

        public override int GetHashCode()
        {
            int hashCode = 1536872568;
            hashCode = hashCode * -1521134295 + EqualityComparer<IModule>.Default.GetHashCode(this.Module);
            hashCode = hashCode * -1521134295 + EqualityComparer<KubernetesModuleIdentity>.Default.GetHashCode(this.ModuleIdentity);
            return hashCode;
        }
    }
}
