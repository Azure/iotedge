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
        public async void TestNullEnvironment()
        {
            NullEnvironment testNullEnvironment = NullEnvironment.Instance;
            Assert.NotNull(testNullEnvironment);

            CancellationToken token = new CancellationToken();
            ModuleSet testModuleSet = await testNullEnvironment.GetModulesAsync(token);

            Assert.NotNull(testModuleSet);
            Assert.Equal(testModuleSet, ModuleSet.Empty);
        }
    }
}