// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Test
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Serde;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Newtonsoft.Json;
    using Xunit;

    public class ModuleSetTest
    {
        static readonly ConfigurationInfo DefaultConfigurationInfo = new ConfigurationInfo("1");
        static readonly IDictionary<string, EnvVal> EnvVars = new Dictionary<string, EnvVal>();
        static readonly TestConfig Config1 = new TestConfig("image1");
        static readonly TestConfig Config2 = new TestConfig("image2");

        static readonly IModule Module1 = new TestModule("mod1", "version1", "test", ModuleStatus.Running, Config1, RestartPolicy.OnUnhealthy, DefaultConfigurationInfo, EnvVars);
        static readonly IModule Module2 = new TestModule("mod2", "version1", "test", ModuleStatus.Running, Config1, RestartPolicy.OnUnhealthy, DefaultConfigurationInfo, EnvVars);
        static readonly IModule Module3 = new TestModule("mod3", "version1", "test", ModuleStatus.Running, Config1, RestartPolicy.OnUnhealthy, DefaultConfigurationInfo, EnvVars);
        static readonly IModule Module4 = new TestModule("mod1", "version1", "test", ModuleStatus.Running, Config2, RestartPolicy.OnUnhealthy, DefaultConfigurationInfo, EnvVars);

        static readonly TestModule Module5 = new TestModule("mod5", "version1", "test", ModuleStatus.Running, Config2, RestartPolicy.OnUnhealthy, DefaultConfigurationInfo, EnvVars);

        static readonly ModuleSet ModuleSet1 = ModuleSet.Create(Module1);
        static readonly ModuleSet ModuleSet2 = ModuleSet.Create(Module5, Module3);

        [Theory]
        [Unit]
        [MemberData(nameof(TestApplyDiffSource.TestData), MemberType = typeof(TestApplyDiffSource))]
        public void TestApplyDiff(ModuleSet starting, Diff diff, ModuleSet expected)
        {
            ModuleSet updated = starting.ApplyDiff(diff);
            Assert.Equal(expected.Modules.Count, updated.Modules.Count);

            foreach (KeyValuePair<string, IModule> module in expected.Modules)
            {
                Assert.True(updated.TryGetModule(module.Key, out IModule updatedMod));
                Assert.Equal(module.Value, updatedMod);
            }
        }

        [Theory]
        [Unit]
        [MemberData(nameof(DiffTestSet.TestData), MemberType = typeof(DiffTestSet))]
        public void TestDiff(ModuleSet start, ModuleSet end, Diff expected)
        {
            Diff setDifference = end.Diff(start);
            Assert.Equal(setDifference, expected);
        }

        [Fact]
        [Unit]
        public void TestDeserialize()
        {
            string validJson = "{\"Modules\":{\"mod1\":{\"Version\":\"version1\",\"Type\":\"test\",\"Status\":\"Running\",\"Settings\":{\"Image\":\"image1\"},\"RestartPolicy\":\"on-unhealthy\",\"Configuration\":{\"id\":\"1\"}},\"mod2\":{\"Version\":\"version1\",\"Type\":\"test\",\"Status\":\"Running\",\"settings\":{\"image\":\"image1\"},\"RestartPolicy\":\"on-unhealthy\",\"configuration\":{\"id\":\"1\"}}}}";
            string validJsonAllLower = "{\"modules\":{\"mod1\":{\"version\":\"version1\",\"type\":\"test\",\"status\":\"running\",\"settings\":{\"image\":\"image1\"},\"restartpolicy\":\"on-unhealthy\",\"Configuration\":{\"id\":\"1\"}},\"mod2\":{\"version\":\"version1\",\"type\":\"test\",\"status\":\"running\",\"settings\":{\"image\":\"image1\"},\"restartpolicy\":\"on-unhealthy\",\"configuration\":{\"id\":\"1\"}}}}";
            string validJsonAllCap = "{\"MODULES\":{\"mod1\":{\"NAME\":\"mod1\",\"VERSION\":\"version1\",\"TYPE\":\"test\",\"STATUS\":\"RUNNING\",\"SETTINGS\":{\"IMAGE\":\"image1\"},\"RESTARTPOLICY\":\"on-unhealthy\",\"Configuration\":{\"id\":\"1\"}},\"mod2\":{\"NAME\":\"mod2\",\"VERSION\":\"version1\",\"TYPE\":\"test\",\"STATUS\":\"RUNNING\",\"SETTINGS\":{\"IMAGE\":\"image1\"},\"RESTARTPOLICY\":\"on-unhealthy\",\"CONFIGURATION\":{\"id\":\"1\"}}}}";

            string noVersionJson = "{\"Modules\":{\"mod1\":{\"Type\":\"test\",\"Status\":\"Running\",\"Settings\":{\"Image\":\"image1\"},\"RestartPolicy\":\"on-unhealthy\",\"Configuration\":{\"id\":\"1\"}},\"mod2\":{\"Type\":\"test\",\"Status\":\"Running\",\"settings\":{\"image\":\"image1\"},\"RestartPolicy\":\"on-unhealthy\",\"configuration\":{\"id\":\"1\"}}}}";
            string noTypeJson = "{\"Modules\":{\"mod1\":{\"Version\":\"version1\",\"Status\":\"Running\",\"Settings\":{\"Image\":\"image1\"},\"RestartPolicy\":\"on-unhealthy\",\"Configuration\":{\"id\":\"1\"}},\"mod2\":{\"Version\":\"version1\",\"Status\":\"Running\",\"settings\":{\"image\":\"image1\"},\"RestartPolicy\":\"on-unhealthy\",\"configuration\":{\"id\":\"1\"}}}}";
            string noStatusJson = "{\"Modules\":{\"mod1\":{\"Version\":\"version1\",\"Type\":\"test\",\"Settings\":{\"Image\":\"image1\"},\"RestartPolicy\":\"on-unhealthy\",\"Configuration\":{\"id\":\"1\"}},\"mod2\":{\"Version\":\"version1\",\"Type\":\"test\",\"settings\":{\"image\":\"image1\"},\"RestartPolicy\":\"on-unhealthy\",\"configuration\":{\"id\":\"1\"}}}}";
            string noConfigJson = "{\"Modules\":{\"mod1\":{\"Version\":\"version1\",\"Type\":\"test\",\"Status\":\"Running\",\"Settings\":{\"Image\":\"image1\"},\"RestartPolicy\":\"on-unhealthy\",\"Configuration\":{\"id\":\"1\"}},\"mod2\":{\"Version\":\"version1\",\"Type\":\"test\",\"Status\":\"Running\",\"RestartPolicy\":\"on-unhealthy\",\"configuration\":{\"id\":\"1\"}}}}";
            string noConfigImageJson = "{\"Modules\":{\"mod1\":{\"Version\":\"version1\",\"Type\":\"test\",\"Status\":\"Running\",\"Settings\":{},\"RestartPolicy\":\"on-unhealthy\",\"Configuration\":{\"id\":\"1\"}},\"mod2\":{\"Version\":\"version1\",\"Type\":\"test\",\"Status\":\"Running\",\"settings\":{},\"RestartPolicy\":\"on-unhealthy\",\"configuration\":{\"id\":\"1\"}}}}";
            string notATestType = "{\"Modules\":{\"mod1\":{\"Version\":\"version1\",\"Type\":\"not_a_test\",\"Status\":\"Running\",\"Settings\":{},\"RestartPolicy\":\"on-unhealthy\",\"Configuration\":{\"id\":\"1\"}},\"mod2\":{\"Version\":\"version1\",\"Type\":\"test\",\"Status\":\"Running\",\"settings\":{},\"RestartPolicy\":\"on-unhealthy\",\"configuration\":{\"id\":\"1\"}}}}";

            var serializerInputTable = new Dictionary<string, Type>() { { "Test", typeof(TestModule) } };
            var myModuleSetSerde = new ModuleSetSerde(serializerInputTable);

            ModuleSet myModuleSet1 = myModuleSetSerde.Deserialize(validJson);
            ModuleSet myModuleSet2 = myModuleSetSerde.Deserialize(validJsonAllLower);
            ModuleSet myModuleSet3 = myModuleSetSerde.Deserialize(validJsonAllCap);

            IModule myModule1 = myModuleSet1.Modules["mod1"];
            IModule myModule2 = myModuleSet1.Modules["mod2"];
            IModule myModule3 = myModuleSet2.Modules["mod1"];
            IModule myModule4 = myModuleSet2.Modules["mod2"];
            IModule myModule5 = myModuleSet3.Modules["mod1"];
            IModule myModule6 = myModuleSet3.Modules["mod2"];

            Assert.True(Module1.Equals(myModule1));
            Assert.True(Module2.Equals(myModule2));
            Assert.True(Module1.Equals(myModule3));
            Assert.True(Module2.Equals(myModule4));
            Assert.True(Module1.Equals(myModule5));
            Assert.True(Module2.Equals(myModule6));

            Assert.Throws<JsonSerializationException>(() => myModuleSetSerde.Deserialize(noVersionJson));
            Assert.Throws<JsonSerializationException>(() => myModuleSetSerde.Deserialize(noStatusJson));
            Assert.Throws<JsonSerializationException>(() => myModuleSetSerde.Deserialize(noTypeJson));
            Assert.Throws<JsonSerializationException>(() => myModuleSetSerde.Deserialize(noConfigJson));
            Assert.Throws<JsonSerializationException>(() => myModuleSetSerde.Deserialize(noConfigImageJson));
            Assert.Throws<JsonSerializationException>(() => myModuleSetSerde.Deserialize(notATestType));
            Assert.Throws<JsonSerializationException>(() => myModuleSetSerde.Deserialize(null));
        }

        [Fact]
        [Unit]
        public void ModuleSetSerialize()
        {
            var serializerInputTable = new Dictionary<string, Type>() { { "test", typeof(TestModule) } };
            var myModuleSetSerde = new ModuleSetSerde(serializerInputTable);

            string jsonFromTestModuleSet = myModuleSetSerde.Serialize(ModuleSet1);
            string jsonFromTestModuleSet2 = myModuleSetSerde.Serialize(ModuleSet2);

            ModuleSet myModuleSet = myModuleSetSerde.Deserialize(jsonFromTestModuleSet);
            ModuleSet myModuleSet2 = myModuleSetSerde.Deserialize(jsonFromTestModuleSet2);

            IModule module1 = ModuleSet1.Modules["mod1"];
            IModule module2 = myModuleSet.Modules["mod1"];

            IModule module3 = ModuleSet2.Modules["mod5"];
            IModule module4 = myModuleSet2.Modules["mod5"];
            IModule module5 = ModuleSet2.Modules["mod3"];
            IModule module6 = myModuleSet2.Modules["mod3"];

            Assert.True(module1.Equals(module2));
            Assert.True(module3.Equals(module4));
            Assert.True(module5.Equals(module6));
        }

        static class DiffTestSet
        {
            static readonly IList<object[]> Data = new List<object[]>
            {
                // adding modules
                new object[]
                {
                    ModuleSet.Empty,
                    ModuleSet.Create(Module1, Module2),
                    new Diff(new List<IModule> { Module1, Module2 }, new List<string>()),
                },
                // removing modules
                new object[]
                {
                    ModuleSet.Create(Module1, Module2),
                    ModuleSet.Empty,
                    new Diff(new List<IModule>(), new List<string>() { "mod1", "mod2" }),
                },
                // change a module
                new object[]
                {
                    ModuleSet.Create(Module2, Module1),
                    ModuleSet.Create(Module4, Module2),
                    new Diff(new List<IModule>() { Module4 }, new List<string>())
                },
                // no changes
                new object[]
                {
                    ModuleSet.Create(Module5, Module3, Module2),
                    ModuleSet.Create(Module2, Module3, Module5),
                    Diff.Empty
                }
            };

            public static IEnumerable<object[]> TestData => Data;
        }

        static class TestApplyDiffSource
        {
            // TODO Add more test cases
            static readonly IList<object[]> Data = new List<object[]>
            {
                new object[]
                {
                    ModuleSet.Create(Module1, Module2),
                    new Diff(new List<IModule> { Module4, Module3 }, new List<string> { "mod2" }),
                    ModuleSet.Create(Module3, Module4)
                },
                new object[]
                {
                    ModuleSet.Empty,
                    Diff.Create(Module2, Module1),
                    ModuleSet.Create(Module1, Module2)
                },
                new object[]
                {
                    ModuleSet.Create(Module1, Module2),
                    new Diff(ImmutableList<IModule>.Empty, new List<string> { "mod2", "mod1" }),
                    ModuleSet.Empty
                },
                new object[]
                {
                    ModuleSet2,
                    Diff.Empty,
                    ModuleSet2
                }
            };

            public static IEnumerable<object[]> TestData => Data;
        }
    }
}
