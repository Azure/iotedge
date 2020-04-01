// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common.Config
{
    using System.Collections.Generic;

    public class ModuleConfiguration
    {
        public IReadOnlyDictionary<string, object> Deployment { get; }

        public IReadOnlyDictionary<string, object> DesiredProperties { get; }

        public string Image { get; }

        public string Name { get; }

        public ModuleConfiguration()
        {
            this.Deployment = new Dictionary<string, object>();
            this.DesiredProperties = new Dictionary<string, object>();
            this.Image = string.Empty;
            this.Name = string.Empty;
        }

        public ModuleConfiguration(
            string name,
            string image,
            IReadOnlyDictionary<string, object> deployment,
            IReadOnlyDictionary<string, object> properties)
        {
            this.Deployment = deployment;
            this.DesiredProperties = properties;
            this.Image = image;
            this.Name = name;
        }

        public bool IsSystemModule()
        {
            return this.Name.StartsWith('$');
        }
    }
}
