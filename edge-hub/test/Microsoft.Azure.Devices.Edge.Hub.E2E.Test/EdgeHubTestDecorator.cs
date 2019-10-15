// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.E2E.Test
{
    public class EdgeHubTestDecorator : EdgeHubTest
    {
        protected EdgeHubTest edgeHubTest;

        public EdgeHubTestDecorator(EdgeHubTest edgeHubTest)
        {
            this.edgeHubTest = edgeHubTest;
        }

        public override void Dispose()
        {
            this.edgeHubTest.Dispose();
        }
    }
}
