// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core.Commands
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;

    public class NullCommandFactory : ICommandFactory
    {
        public static NullCommandFactory Instance { get; } = new NullCommandFactory();

        static NullCommand Command { get; } = new NullCommand();

        NullCommandFactory()
        {
        }

        public ICommand Create(IModule module) => Command;

        public ICommand Update(IModule current, IModule next) => Command;

        public ICommand Remove(IModule module) => Command;

        public ICommand Start(IModule module) => Command;

        public ICommand Stop(IModule module) => Command;

        class NullCommand : ICommand
        {
            public Task ExecuteAsync(CancellationToken token) => TaskEx.Done;

            public Task UndoAsync(CancellationToken token) => TaskEx.Done;
        }
    }
}