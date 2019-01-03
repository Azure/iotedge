// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using Microsoft.Azure.Devices.Edge.Util;

    public class Plan
    {
        public Plan(IList<ICommand> commands)
        {
            this.Commands = Preconditions.CheckNotNull(commands.ToImmutableList(), nameof(commands));
        }

        public static Plan Empty { get; } = new Plan(ImmutableList<ICommand>.Empty);

        public bool IsEmpty => this.Commands.IsEmpty;

        public ImmutableList<ICommand> Commands { get; }
    }
}
