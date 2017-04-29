// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core.Commands
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;

    public class NullCommand : ICommand
    {
        public static NullCommand Instance { get; } = new NullCommand();

        NullCommand()
        {
        }

        public Task ExecuteAsync(CancellationToken token) => TaskEx.Done;

        public Task UndoAsync(CancellationToken token) => TaskEx.Done;

        public string Show() => "[Null]";
    }
}