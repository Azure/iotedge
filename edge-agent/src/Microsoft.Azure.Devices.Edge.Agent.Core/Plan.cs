// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;

    public class Plan
    {
        readonly ImmutableList<ICommand> commands;

        public static Plan Empty { get; } = new Plan(ImmutableList<ICommand>.Empty);

        public bool IsEmpty => this.commands.IsEmpty;

        public Plan(IList<ICommand> commands)
        {
            this.commands = Preconditions.CheckNotNull(commands.ToImmutableList(), nameof(commands));
        }

        public async Task ExecuteAsync(CancellationToken token)
        {
            foreach (ICommand command in this.commands)
            {
                // TODO add rollback on failure?
                await command.ExecuteAsync(token);
            }
        }
    }
}