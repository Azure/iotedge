// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core.Test
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Newtonsoft.Json;
    using Xunit;

    public class ModuleSetTest
    {
        static readonly TestConfig Config1 = new TestConfig("image1");
        static readonly TestConfig Config2 = new TestConfig("image2");

        static readonly IModule Module1 = new TestModule("mod1", "version1", "test", ModuleStatus.Running, Config1);
        static readonly IModule Module2 = new TestModule("mod2", "version1", "test", ModuleStatus.Running, Config1);
        static readonly IModule Module3 = new TestModule("mod3", "version1", "test", ModuleStatus.Running, Config1);
        static readonly IModule Module4 = new TestModule("mod1", "version1", "test", ModuleStatus.Running, Config2);

        static readonly TestModule Module5 = new TestModule("mod5", "version1", "test", ModuleStatus.Running, Config2);

        static readonly ModuleSet ModuleSet1 = ModuleSet.Create(Module1);
        static readonly ModuleSet ModuleSet2 = ModuleSet.Create(Module5, Module3);

        static class TestApplyDiffSource
        {
            public static IEnumerable<object[]> TestData => Data;

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
            };
        }

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

        [Fact]
        [Unit]
        public void TestDeserialize()
        {
            string validJson = "{\"Modules\":{\"mod1\":{\"Name\":\"mod1\",\"Version\":\"version1\",\"Type\":\"test\",\"Status\":\"Running\",\"Config\":{\"Image\":\"image1\"}},\"mod2\":{\"Name\":\"mod2\",\"Version\":\"version1\",\"Type\":\"test\",\"Status\":\"Running\",\"config\":{\"image\":\"image1\"}}}}";
            string validJsonAllLower = "{\"modules\":{\"mod1\":{\"name\":\"mod1\",\"version\":\"version1\",\"type\":\"test\",\"status\":\"running\",\"config\":{\"image\":\"image1\"}},\"mod2\":{\"name\":\"mod2\",\"version\":\"version1\",\"type\":\"test\",\"status\":\"running\",\"config\":{\"image\":\"image1\"}}}}";
            string validJsonAllCap = "{\"MODULES\":{\"mod1\":{\"NAME\":\"mod1\",\"VERSION\":\"version1\",\"TYPE\":\"test\",\"STATUS\":\"RUNNING\",\"CONFIG\":{\"IMAGE\":\"image1\"}},\"mod2\":{\"NAME\":\"mod2\",\"VERSION\":\"version1\",\"TYPE\":\"test\",\"STATUS\":\"RUNNING\",\"CONFIG\":{\"IMAGE\":\"image1\"}}}}";

            string noNameson = "{\"Modules\":{\"mod1\":{\"Version\":\"version1\",\"Type\":\"test\",\"Status\":\"Running\",\"Config\":{\"Image\":\"image1\"}},\"mod2\":{,\"Version\":\"version1\",\"Type\":\"test\",\"Status\":\"Running\",\"config\":{\"image\":\"image1\"}}}}";
            string noVersionJson = "{\"Modules\":{\"mod1\":{\"Name\":\"mod1\",\"Type\":\"test\",\"Status\":\"Running\",\"Config\":{\"Image\":\"image1\"}},\"mod2\":{\"Name\":\"mod2\",\"Type\":\"test\",\"Status\":\"Running\",\"config\":{\"image\":\"image1\"}}}}";
            string noTypeJson = "{\"Modules\":{\"mod1\":{\"Name\":\"mod1\",\"Version\":\"version1\",\"Status\":\"Running\",\"Config\":{\"Image\":\"image1\"}},\"mod2\":{\"Name\":\"mod2\",\"Version\":\"version1\",\"Status\":\"Running\",\"config\":{\"image\":\"image1\"}}}}";
            string noStatusJson = "{\"Modules\":{\"mod1\":{\"Name\":\"mod1\",\"Version\":\"version1\",\"Type\":\"test\",\"Config\":{\"Image\":\"image1\"}},\"mod2\":{\"Name\":\"mod2\",\"Version\":\"version1\",\"Type\":\"test\",\"config\":{\"image\":\"image1\"}}}}";
            string noConfigJson = "{\"Modules\":{\"mod1\":{\"Name\":\"mod1\",\"Version\":\"version1\",\"Type\":\"test\",\"Status\":\"Running\",\"Config\":{\"Image\":\"image1\"}},\"mod2\":{\"Name\":\"mod2\",\"Version\":\"version1\",\"Type\":\"test\",\"Status\":\"Running\",}}}";
            string noConfigImageJson = "{\"Modules\":{\"mod1\":{\"Name\":\"mod1\",\"Version\":\"version1\",\"Type\":\"test\",\"Status\":\"Running\",\"Config\":{}},\"mod2\":{\"Name\":\"mod2\",\"Version\":\"version1\",\"Type\":\"test\",\"Status\":\"Running\",\"config\":{}}}}";

            var serializerInputTable = new Dictionary<string, Type>() { { "Test", typeof(TestModule) }};
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

            Assert.Throws<JsonSerializationException>(() => myModuleSetSerde.Deserialize(noNameson));
            Assert.Throws<JsonSerializationException>(() => myModuleSetSerde.Deserialize(noVersionJson));
            Assert.Throws<JsonSerializationException>(() => myModuleSetSerde.Deserialize(noStatusJson));
            Assert.Throws<JsonSerializationException>(() => myModuleSetSerde.Deserialize(noTypeJson));
            Assert.Throws<JsonSerializationException>(() => myModuleSetSerde.Deserialize(noConfigJson));
            Assert.Throws<JsonSerializationException>(() => myModuleSetSerde.Deserialize(noConfigImageJson));
        }

        [Fact]
        [Unit]
        public void ModuleSetSerialize()
        {
            var serializerInputTable = new Dictionary<string, Type>() {{"test", typeof(TestModule)}};
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
    }
}