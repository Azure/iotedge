// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Test
{
    using System.Threading;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;

    public class NullEnvironmentTest
    {
        [Fact]
        [Unit]
        public async void TestNullEnvironment()
        {
            NullEnvironment testNullEnvironment = NullEnvironment.Instance;
            Assert.NotNull(testNullEnvironment);

            var token = new CancellationToken();
            ModuleSet testModuleSet = await testNullEnvironment.GetModulesAsync(token);

            Assert.NotNull(testModuleSet);
            Assert.Equal(testModuleSet, ModuleSet.Empty);

            var inputRuntimeInfo = new Mock<IRuntimeInfo>();
            IRuntimeInfo runtimeInfo = await testNullEnvironment.GetUpdatedRuntimeInfoAsync(inputRuntimeInfo.Object);
            Assert.NotNull(runtimeInfo);
            Assert.True(runtimeInfo == inputRuntimeInfo.Object);
        }
    }
}
