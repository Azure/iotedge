// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core.Commands
{
    public class NullCommandFactory : ICommandFactory
    {
        public static NullCommandFactory Instance { get; } = new NullCommandFactory();

        NullCommandFactory()
        {
        }

        public ICommand Create(IModuleWithIdentity module) => NullCommand.Instance;

        public ICommand Pull(IModule module) => NullCommand.Instance;

        public ICommand Update(IModule current, IModuleWithIdentity next) => NullCommand.Instance;

        public ICommand Remove(IModule module) => NullCommand.Instance;

        public ICommand Start(IModule module) => NullCommand.Instance;

        public ICommand Stop(IModule module) => NullCommand.Instance;

        public ICommand Restart(IModule module) => NullCommand.Instance;

        public ICommand Wrap(ICommand command) => command;
    }
}
