// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Test.Commands
{
    using System.Threading;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Commands;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    public class NullCommandTest
    {
        [Fact]
        [Unit]
        public async void NullCommandTestAll()
        {
            NullCommand n = NullCommand.Instance;
            var token = default(CancellationToken);

            await n.ExecuteAsync(token);

            await n.UndoAsync(token);

            Assert.Equal("[Null]", n.Show());
        }
    }
}
