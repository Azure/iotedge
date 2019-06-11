// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common.Config
{
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Util;

    public class ModuleConfiguration
    {
        public IReadOnlyDictionary<string, object> Deployment { get; }

        public IReadOnlyDictionary<string, object> DesiredProperties { get; }

        public string Name { get; }

        public ModuleConfiguration(string name, IReadOnlyDictionary<string, object> deployment, IReadOnlyDictionary<string, object> properties)
        {
            this.Deployment = Preconditions.CheckNotNull(deployment, nameof(deployment));
            this.DesiredProperties = Preconditions.CheckNotNull(properties, nameof(properties));
            this.Name = Preconditions.CheckNonWhiteSpace(name, nameof(name));
        }
    }
}
