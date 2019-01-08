// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core.Test
{
    using System;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Routing.Core.Util;
    using Xunit;

    public class SinkResultTest : RoutingUnitTestBase
    {
        [Fact]
        [Unit]
        public void TestConstructor()
        {
            var result1 = new SinkResult<int>(new[] { 1 }, new[] { 2 }, null);
            Assert.Equal(new[] { 1 }, result1.Succeeded);
            Assert.Equal(new[] { 2 }, result1.Failed);
            Assert.Equal(Option.None<SendFailureDetails>(), result1.SendFailureDetails);

            var result2 = new SinkResult<int>(new[] { 3 }, new[] { 4 }, new SendFailureDetails(FailureKind.InternalError, new ArgumentNullException()));
            Assert.Equal(new[] { 3 }, result2.Succeeded);
            Assert.Equal(new[] { 4 }, result2.Failed);
            Assert.True(result2.SendFailureDetails.HasValue);
            result2.SendFailureDetails.ForEach(ex => Assert.IsType<ArgumentNullException>(ex.RawException));
        }
    }
}
