// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Test
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    public class NullEnvironmentTest
    {
        [Fact]
        [Unit]
        public async Task TestNullEnvironment()
        {
            NullEnvironment testNullEnvironment = NullEnvironment.Instance;
            Assert.NotNull(testNullEnvironment);

            var token = default(CancellationToken);
            ModuleSet testModuleSet = await testNullEnvironment.GetModulesAsync(token);

            Assert.NotNull(testModuleSet);
            Assert.Equal(testModuleSet, ModuleSet.Empty);

            IRuntimeInfo runtimeInfo = await testNullEnvironment.GetRuntimeInfoAsync();
            Assert.NotNull(runtimeInfo);
            Assert.True(ReferenceEquals(runtimeInfo, UnknownRuntimeInfo.Instance));
        }
    }
}
