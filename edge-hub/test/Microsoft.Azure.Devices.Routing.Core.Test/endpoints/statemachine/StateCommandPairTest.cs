// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Routing.Core.Test.Endpoints.StateMachine
{
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Routing.Core.Endpoints.StateMachine;

    using Xunit;

    public class StateCommandPairTest : RoutingUnitTestBase
    {
        [Fact]
        [Unit]
        public void TestEqual()
        {
            var pair1 = new StateCommandPair(State.Idle, CommandType.SendMessage);
            var pair2 = new StateCommandPair(State.Idle, CommandType.SendMessage);
            var pair3 = new StateCommandPair(State.Failing, CommandType.SendMessage);
            var pair4 = new StateCommandPair(State.Idle, CommandType.UpdateEndpoint);

            Assert.Equal(pair1, pair1);
            Assert.Equal(pair1, pair2);
            Assert.True(pair1 == pair2);
            Assert.True(pair1 != pair3);
            Assert.True(pair1.Equals((object)pair2));
            Assert.NotEqual(pair1, pair3);
            Assert.NotEqual(pair1, pair4);
            Assert.False(pair1.Equals(new object()));
        }

        [Fact]
        [Unit]
        public void TestShow()
        {
            var pair = new StateCommandPair(State.Idle, CommandType.SendMessage);
            Assert.Equal("StateCommandPair(Idle, SendMessage)", pair.ToString());
        }
    }
}
