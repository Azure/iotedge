// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Edgelet
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Commands;
    using Microsoft.Azure.Devices.Edge.Agent.Edgelet.Commands;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Configuration;

    public class EdgeletCommandFactory<T> : ICommandFactory
    {
        readonly IConfigSource configSource;
        readonly IModuleManager moduleManager;
        readonly ICombinedConfigProvider<T> combinedConfigProvider;
        readonly string edgeDeviceHostname;
        readonly Option<string> parentEdgeHostname;

        public EdgeletCommandFactory(
            IModuleManager moduleManager,
            IConfigSource configSource,
            ICombinedConfigProvider<T> combinedConfigProvider)
        {
            this.moduleManager = Preconditions.CheckNotNull(moduleManager, nameof(moduleManager));
            this.configSource = Preconditions.CheckNotNull(configSource, nameof(configSource));
            this.combinedConfigProvider = Preconditions.CheckNotNull(combinedConfigProvider, nameof(combinedConfigProvider));
            this.edgeDeviceHostname = this.configSource.Configuration.GetValue<string>(Constants.EdgeDeviceHostNameKey);
            if (string.IsNullOrWhiteSpace(this.edgeDeviceHostname))
            {
                throw new ArgumentException("EdgeDeviceHostname from configuration is null, empty or whitespaces.");
            }

            this.parentEdgeHostname = Option.Maybe(this.configSource.Configuration.GetValue<string>(Constants.GatewayHostnameVariableName));
        }

        public Task<ICommand> CreateAsync(IModuleWithIdentity module, IRuntimeInfo runtimeInfo) =>
            Task.FromResult(
                CreateOrUpdateCommand.BuildCreate(
                    this.moduleManager,
                    module.Module,
                    module.ModuleIdentity,
                    this.configSource,
                    this.combinedConfigProvider.GetCombinedConfig(module.Module, runtimeInfo),
                    this.edgeDeviceHostname,
                    this.parentEdgeHostname)
                as ICommand);

        public Task<ICommand> UpdateAsync(IModule current, IModuleWithIdentity next, IRuntimeInfo runtimeInfo) =>
            this.UpdateAsync(Option.Some(current), next, runtimeInfo, false);

        public Task<ICommand> UpdateEdgeAgentAsync(IModuleWithIdentity module, IRuntimeInfo runtimeInfo) =>
            this.UpdateAsync(Option.None<IModule>(), module, runtimeInfo, true);

        public Task<ICommand> RemoveAsync(IModule module) => Task.FromResult(new RemoveCommand(this.moduleManager, module) as ICommand);

        public Task<ICommand> StartAsync(IModule module) => Task.FromResult(new StartCommand(this.moduleManager, module) as ICommand);

        public Task<ICommand> StopAsync(IModule module) => Task.FromResult(new StopCommand(this.moduleManager, module) as ICommand);

        public Task<ICommand> RestartAsync(IModule module) => Task.FromResult(new RestartCommand(this.moduleManager, module) as ICommand);

        public Task<ICommand> WrapAsync(ICommand command) => Task.FromResult(command);

        async Task<ICommand> UpdateAsync(Option<IModule> current, IModuleWithIdentity next, IRuntimeInfo runtimeInfo, bool start)
        {
            T config = this.combinedConfigProvider.GetCombinedConfig(next.Module, runtimeInfo);
            return new GroupCommand(
                new PrepareUpdateCommand(this.moduleManager, next.Module, config),
                await current.Match(this.StopAsync, () => Task.FromResult<ICommand>(NullCommand.Instance)),
                CreateOrUpdateCommand.BuildUpdate(
                    this.moduleManager,
                    next.Module,
                    next.ModuleIdentity,
                    this.configSource,
                    config,
                    start,
                    this.edgeDeviceHostname,
                    this.parentEdgeHostname)
                as ICommand);
        }
    }
}
