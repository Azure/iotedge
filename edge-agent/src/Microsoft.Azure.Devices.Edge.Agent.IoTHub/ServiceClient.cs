// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;

    public class ServiceClient : IServiceClient
    {
        readonly RegistryManager rm;
        readonly string deviceId;

        public ServiceClient(EdgeHubConnectionString connectionDetails)
        {
            Preconditions.CheckNotNull(connectionDetails, nameof(connectionDetails));

            this.deviceId = Preconditions.CheckNotNull(connectionDetails.DeviceId, nameof(this.deviceId));
            string connectionString = new EdgeHubConnectionString.EdgeHubConnectionStringBuilder(connectionDetails.HostName, connectionDetails.DeviceId)
                .SetSharedAccessKey(connectionDetails.SharedAccessKey).Build().ToConnectionString();
            this.rm = RegistryManager.CreateFromConnectionString(connectionString);
        }

        public void Dispose()
        {
            this.rm.Dispose();
        }

        public Task<IEnumerable<Module>> GetModules()
        {
            return this.rm.GetModulesOnDeviceAsync(this.deviceId);
        }

        public Task<Module> GetModule(string moduleId)
        {
            Preconditions.CheckNonWhiteSpace(moduleId, nameof(moduleId));
            return this.rm.GetModuleAsync(this.deviceId, moduleId);
        }

        public Task<Module[]> CreateModules(IEnumerable<string> identities)
        {
            return Task.WhenAll(identities.Select(moduleId => this.rm.AddModuleAsync(new Module(this.deviceId, moduleId))));
        }

        public Task<Module[]> UpdateModules(IEnumerable<Module> modules)
        {
            IList<Task<Module>> updateTasks = new List<Task<Module>>();
            foreach (Module module in modules)
            {
                updateTasks.Add(this.rm.UpdateModuleAsync(module));
            }

            return Task.WhenAll(updateTasks);
        }

        public Task RemoveModules(IEnumerable<string> identities)
        {
            return Task.WhenAll(identities.Select(moduleId => this.rm.RemoveModuleAsync(this.deviceId, moduleId)));
        }
    }
}
