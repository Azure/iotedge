// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Planner
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Commands;
    using Microsoft.Azure.Devices.Edge.Util;

    public class CreateUpdateCommandEntity
    {
        public Option<Task<ICommand>> UpfrontImagePullCommand { get; }
        public Task<ICommand> CreateUpdateCommand { get; }

        public CreateUpdateCommandEntity(Option<Task<ICommand>> upfrontImagePullCommand, Task<ICommand> createUpdateCommand)
        {
            this.UpfrontImagePullCommand = upfrontImagePullCommand;
            this.CreateUpdateCommand = createUpdateCommand;
        }
    }

    public interface ICreateUpdateCommandFactory
    {
        Task<CreateUpdateCommandEntity> GenerateCreateCommands(IModuleWithIdentity module, IRuntimeInfo runtimeInfo);
        Task<CreateUpdateCommandEntity> GenerateUpdateCommands(IModule currentModule, IModuleWithIdentity module, IRuntimeInfo runtimeInfo);
    }

    public class UpfrontImagePullCommandFactory : ICreateUpdateCommandFactory
    {
        ICommandFactory commandFactory;

        public UpfrontImagePullCommandFactory(ICommandFactory commandFactory)
        {
            this.commandFactory = commandFactory;
        }

        public Task<CreateUpdateCommandEntity> GenerateCreateCommands(IModuleWithIdentity module, IRuntimeInfo runtimeInfo)
        {
            Task<ICommand> prepareUpdate = this.commandFactory.PrepareUpdateAsync(module.Module, runtimeInfo);
            Task<ICommand> createCommand = this.commandFactory.CreateAsync(module, runtimeInfo);
            CreateUpdateCommandEntity output = new CreateUpdateCommandEntity(Option.Some(prepareUpdate), createCommand);
            return Task.FromResult(output);
        }

        public Task<CreateUpdateCommandEntity> GenerateUpdateCommands(IModule currentModule, IModuleWithIdentity module, IRuntimeInfo runtimeInfo)
        {
            Task<ICommand> prepareUpdate = this.commandFactory.PrepareUpdateAsync(module.Module, runtimeInfo);
            Task<ICommand> updateCommand = this.commandFactory.UpdateAsync(currentModule, module, runtimeInfo);
            CreateUpdateCommandEntity output = new CreateUpdateCommandEntity(Option.Some(prepareUpdate), updateCommand);
            return Task.FromResult(output);
        }
    }

    public class StandardCommandFactory : ICreateUpdateCommandFactory
    {
        ICommandFactory commandFactory;

        public StandardCommandFactory(ICommandFactory commandFactory)
        {
            this.commandFactory = commandFactory;
        }

        public Task<CreateUpdateCommandEntity> GenerateCreateCommands(IModuleWithIdentity module, IRuntimeInfo runtimeInfo)
        {
            Task<ICommand> createCommand = this.commandFactory.CreateAsync(module, runtimeInfo);
            return this.GenerateCreateUpdateCommandImpl(createCommand, module, runtimeInfo);
        }

        public Task<CreateUpdateCommandEntity> GenerateUpdateCommands(IModule currentModule, IModuleWithIdentity module, IRuntimeInfo runtimeInfo)
        {
            Task<ICommand> updateCommand = this.commandFactory.UpdateAsync(currentModule, module, runtimeInfo);
            return this.GenerateCreateUpdateCommandImpl(updateCommand, module, runtimeInfo);
        }

        async Task<CreateUpdateCommandEntity> GenerateCreateUpdateCommandImpl(Task<ICommand> createUpdateCommand, IModuleWithIdentity module, IRuntimeInfo runtimeInfo)
        {
            Task<ICommand> prepareUpdate = this.commandFactory.PrepareUpdateAsync(module.Module, runtimeInfo);

            // Command needs to be grouped so that image pull is
            // guaranteed to succeed before we issue a create command.
            // This prevents multiple create commands from getting
            // executed within aziot-edged if EdgeAgent timesout
            // create request and reissues.
            //
            // Multiple create requests being executed within
            // aziot-edged will lead to race condition with workload
            // socket creation and removal.
            IList<Task<ICommand>> cmds = new List<Task<ICommand>> { prepareUpdate, createUpdateCommand };
            Task<ICommand> createOrUpdateWithPull = this.commandFactory.WrapAsync(new GroupCommand(await Task.WhenAll(cmds)));

            CreateUpdateCommandEntity output = new CreateUpdateCommandEntity(Option.None<Task<ICommand>>(), createOrUpdateWithPull);
            return output;
        }
    }
}
