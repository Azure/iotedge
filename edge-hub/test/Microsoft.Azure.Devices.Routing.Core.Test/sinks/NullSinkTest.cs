// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Routing.Core.Test.Sinks
{
    using System.Diagnostics.CodeAnalysis;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Routing.Core.Sinks;

    using Xunit;

    [ExcludeFromCodeCoverage]
    public class NullSinkTest : RoutingUnitTestBase
    {
        [Fact]
        [Unit]
        public void SmokeTask()
        {
            var sink = new NullSink<int>();
            Task result = sink.ProcessAsync(1, CancellationToken.None);
            Assert.True(result.IsCompleted);

            Task result2 = sink.ProcessAsync(new[] { 1, 2 }, CancellationToken.None);
            Assert.True(result2.IsCompleted);

            Task result3 = sink.CloseAsync(CancellationToken.None);
            Assert.True(result3.IsCompleted);
        }
    }
}
