// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common.Config
{
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.Devices.Edge.Util;

    public class EdgeConfigBuilder
    {
        readonly string deviceId;
        readonly IotHub iotHub;
        readonly List<IModuleConfigBuilder> moduleBuilders;
        Option<(string address, string username, string password)> registry;

        public EdgeConfigBuilder(string deviceId, IotHub iotHub)
        {
            this.deviceId = deviceId;
            this.iotHub = iotHub;
            this.moduleBuilders = new List<IModuleConfigBuilder>();
            this.registry = Option.None<(string, string, string)>();
        }

        public void AddRegistryCredentials(string address, string username, string password)
        {
            Preconditions.CheckNonWhiteSpace(address, nameof(address));
            Preconditions.CheckNonWhiteSpace(username, nameof(username));
            Preconditions.CheckNonWhiteSpace(password, nameof(password));

            this.registry = Option.Some((address, username, password));
        }

        public IModuleConfigBuilder AddEdgeAgent(string image = null)
        {
            // `image` cannot be empty. Builder will replace null with default.
            Option<string> imageOption = Option.Maybe(image);
            imageOption.ForEach(i => Preconditions.CheckNonWhiteSpace(i, nameof(i)));
            this.moduleBuilders.Add(new AgentModuleConfigBuilder(imageOption));
            return this.moduleBuilders.Last();
        }

        public IModuleConfigBuilder AddEdgeHub(string image = null)
        {
            // `image` cannot be empty. Builder will replace null with default.
            Option<string> imageOption = Option.Maybe(image);
            imageOption.ForEach(i => Preconditions.CheckNonWhiteSpace(i, nameof(i)));
            this.moduleBuilders.Add(new HubModuleConfigBuilder(imageOption));
            return this.moduleBuilders.Last();
        }

        public IModuleConfigBuilder AddModule(string name, string image)
        {
            Preconditions.CheckNonWhiteSpace(name, nameof(name));
            Preconditions.CheckNonWhiteSpace(image, nameof(image));
            this.moduleBuilders.Add(new ModuleConfigBuilder(name, image));
            return this.moduleBuilders.Last();
        }

        public EdgeConfiguration Build()
        {
            // if caller didn't add $edgeAgent already, add it here with defaults
            if (!this.moduleBuilders.Any(m => m.Name == "edgeAgent" && m.System))
            {
                this.AddEdgeAgent();
            }

            var modules = this.moduleBuilders.Select(b => b.Build());
            return new EdgeConfiguration(this.deviceId, modules, this.registry, this.iotHub);
        }
    }
}
