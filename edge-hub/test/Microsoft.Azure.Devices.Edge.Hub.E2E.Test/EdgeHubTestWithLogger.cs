// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.E2E.Test
{
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit.Abstractions;

    public class EdgeHubTestWithLogger : EdgeHubTestDecorator
    {
        TestConsoleLogger logger;

        public EdgeHubTestWithLogger(EdgeHubTest edgeHubTest, ITestOutputHelper testOutputHelper)
            : base(edgeHubTest)
        {
            this.logger = new TestConsoleLogger(testOutputHelper);
        }

        public override void Dispose()
        {
            this.logger.Dispose();
            base.Dispose();
        }
    }
}
