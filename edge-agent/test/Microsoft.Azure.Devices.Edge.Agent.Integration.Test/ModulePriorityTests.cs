// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Integration.Test
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    public class ModulePriorityTests : AgentTestsBase
    {
        const string TestConfig = "module-priority-test-config.json";

        [Integration]
        [Theory]
        [MemberData(nameof(AgentTestsBase.GenerateStartTestData), TestConfig)]
        public async Task ModulesProcessedInPriorityOrderAsync(TestConfig testConfig)
        {
            await this.AgentExecutionTestAsync(testConfig);
        }
    }
}
