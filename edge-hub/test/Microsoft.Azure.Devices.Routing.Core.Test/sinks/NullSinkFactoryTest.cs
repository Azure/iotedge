// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core.Test.Sinks
{
    using System.Diagnostics.CodeAnalysis;
    using Microsoft.Azure.Devices.Routing.Core.Sinks;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    [ExcludeFromCodeCoverage]
    public class NullSinkFactoryTest : RoutingUnitTestBase
    {
        [Fact, Unit]
        public void SmokeTest()
        {
            var factory = new NullSinkFactory<int>();
            ISink<int> sink = factory.CreateAsync("hub").Result;
            Assert.IsType<NullSink<int>>(sink);
        }
    }
}