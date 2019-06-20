// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using Newtonsoft.Json;

    public class UnknownModule : IModule
    {
        public virtual string Type => Constants.Unknown;

        public virtual string Name
        {
            get => Constants.Unknown;
            set { }
        }

        public virtual string Version => string.Empty;

        public virtual ModuleStatus DesiredStatus => ModuleStatus.Unknown;

        public virtual RestartPolicy RestartPolicy => RestartPolicy.Never;

        public virtual ImagePullPolicy ImagePullPolicy => ImagePullPolicy.OnCreate;

        public virtual ConfigurationInfo ConfigurationInfo => new ConfigurationInfo();

        public IDictionary<string, EnvVal> Env { get; } = ImmutableDictionary<string, EnvVal>.Empty;

        public bool OnlyModuleStatusChanged(IModule other) => other is UnknownModule;

        public bool Equals(IModule other) => other != null && ReferenceEquals(this, other);
    }

    public class UnknownEdgeHubModule : UnknownModule, IEdgeHubModule
    {
        UnknownEdgeHubModule()
        {
        }

        public static UnknownEdgeHubModule Instance { get; } = new UnknownEdgeHubModule();

        [JsonIgnore]
        public override string Version => string.Empty;

        public IModule WithRuntimeStatus(ModuleStatus newStatus) => Instance;
    }

    public class UnknownEdgeAgentModule : UnknownModule, IEdgeAgentModule
    {
        UnknownEdgeAgentModule()
        {
        }

        public static UnknownEdgeAgentModule Instance { get; } = new UnknownEdgeAgentModule();

        [JsonIgnore]
        public override string Version => string.Empty;

        [JsonIgnore]
        public override RestartPolicy RestartPolicy => RestartPolicy.Never;

        [JsonIgnore]
        public override ImagePullPolicy ImagePullPolicy => ImagePullPolicy.OnCreate;

        [JsonIgnore]
        public override ModuleStatus DesiredStatus => ModuleStatus.Unknown;

        public IModule WithRuntimeStatus(ModuleStatus newStatus) => new UnknownEdgeAgentModule();
    }
}
