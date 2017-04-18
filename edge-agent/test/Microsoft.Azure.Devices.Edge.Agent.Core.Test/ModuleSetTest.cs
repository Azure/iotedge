// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core.Test
{
    using System.Collections.Generic;
    using Xunit;

    public class ModuleSetTest
    {
        static readonly IModule Module1 = new TestModule("mod1", "type1");
        static readonly IModule Module2 = new TestModule("mod2", "type1");
        static readonly IModule Module3 = new TestModule("mod3", "type1");
        static readonly IModule Module4 = new TestModule("mod1", "type2");

        static class TestApplyDiffSource
        {
            public static IEnumerable<object[]> TestData => Data;

            // TODO Add more test cases
            static readonly IList<object[]> Data = new List<object[]>
            {
                new object[] { ModuleSet.Create(Module1, Module2), new Diff(new List<IModule> { Module4, Module3 }, new List<string> { "mod2" }), ModuleSet.Create(Module3, Module4) },
            };
        }

        [Theory]
        [MemberData(nameof(TestApplyDiffSource.TestData), MemberType = typeof(TestApplyDiffSource))]
        public void TestApplyDiff(ModuleSet starting, Diff diff, ModuleSet expected)
        {
            ModuleSet updated = starting.ApplyDiff(diff);
            Assert.Equal(expected.Modules.Count, updated.Modules.Count);

            foreach (IModule module in expected.Modules)
            {
                Assert.True(updated.TryGetModule(module.Name, out IModule updatedMod));
                Assert.Equal(module.Type, updatedMod.Type);
            }
        }
    }
}