// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common.Config
{
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using Microsoft.Azure.Devices.Edge.Util;

    public class ModuleConfigurationBase
    {
        public string Name { get; }

        public bool System { get; }

        public ModuleConfigurationBase(string name, bool system)
        {
            this.Name = Preconditions.CheckNonWhiteSpace(name, nameof(name));
            this.System = system;
        }
    }

    public class ModuleConfigurationInternal : ModuleConfigurationBase
    {
        public IDictionary<string, object> DesiredProperties { get; }

        public IDictionary<string, object> Deployment { get; }

        public ModuleConfigurationInternal(string name, bool system, IDictionary<string, object> deployment, IDictionary<string, object> properties)
            : base(name, system)
        {
            this.Deployment = Preconditions.CheckNotNull(deployment, nameof(deployment));
            this.DesiredProperties = Preconditions.CheckNotNull(properties, nameof(properties));
        }
    }

    public class ModuleConfiguration : ModuleConfigurationBase
    {
        public IReadOnlyDictionary<string, object> Deployment { get; }

        public IReadOnlyDictionary<string, object> DesiredProperties { get; }

        public ModuleConfiguration(ModuleConfigurationInternal config)
            : base(config.Name, config.System)
        {
            this.Deployment = new ReadOnlyDictionary<string, object>(config.Deployment);
            this.DesiredProperties = new ReadOnlyDictionary<string, object>(config.DesiredProperties);
        }
    }
}
