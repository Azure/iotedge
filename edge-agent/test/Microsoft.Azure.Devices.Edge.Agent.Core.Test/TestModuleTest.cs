// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core.Test
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Serde;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Newtonsoft.Json;
    using Xunit;

    [ExcludeFromCodeCoverage]
    public class TestModuleTest
    {
        static readonly TestConfig Config1 = new TestConfig("image1");
        static readonly TestConfig Config2 = new TestConfig("image2");
        static readonly TestConfig Config3 = new TestConfig("image1");

        static readonly IModule Module1 = new TestModule("mod1", "version1", "type1", ModuleStatus.Running, Config1);
        static readonly IModule Module2 = new TestModule("mod1", "version1", "type1", ModuleStatus.Running, Config1);
        static readonly IModule Module3 = new TestModule("mod3", "version1", "type1", ModuleStatus.Running, Config1);
        static readonly IModule Module4 = new TestModule("mod1", "version2", "type1", ModuleStatus.Running, Config1);
        static readonly IModule Module5 = new TestModule("mod1", "version1", "type2", ModuleStatus.Running, Config1);
        static readonly IModule Module6 = new TestModule("mod1", "version1", "type1", ModuleStatus.Unknown, Config1);
        static readonly IModule Module7 = new TestModule("mod1", "version1", "type1", ModuleStatus.Running, Config2);
        static readonly TestModule Module8 = new TestModule("mod1", "version1", "type1", ModuleStatus.Running, Config1);

        static readonly IModule ValidJsonModule = new TestModule("<module_name>", "<semantic_version_number>", "docker", ModuleStatus.Running, Config1);
        static readonly string serializedModule = "{\"name\":\"mod1\",\"version\":\"version1\",\"type\":\"type1\",\"status\":\"running\",\"config\":{\"image\":\"image1\"}}";

        [Fact]
        [Unit]
        public void TestConstructor()
        {
            Assert.Throws<ArgumentNullException>(() => new TestModule(null, "version1", "type1", ModuleStatus.Running, Config1));
            Assert.Throws<ArgumentNullException>(() => new TestModule("mod1", null, "type1", ModuleStatus.Running, Config1));
            Assert.Throws<ArgumentNullException>(() => new TestModule("mod1", "version1", null, ModuleStatus.Running, Config1));
            Assert.Throws<ArgumentNullException>(() => new TestModule("mod1", "version1", "type1", ModuleStatus.Running, null));
        }

        [Fact]
        [Unit]
        public void TestEquality()
        {
            Assert.Equal(Module1, Module1);
            Assert.Equal(Module1, Module2);
            Assert.Equal(Module8, Module8);
            Assert.NotEqual(Module1, Module3);
            Assert.NotEqual(Module1, Module4);
            Assert.NotEqual(Module1, Module5);
            Assert.NotEqual(Module1, Module6);
            Assert.NotEqual(Module1, Module7);
            Assert.Equal(Module1, Module8);

            Assert.False(Module1.Equals(null));
            Assert.False(Module8.Equals(null));

            Assert.True(Module1.Equals(Module1));
            Assert.False(Module1.Equals(Module3));

            Assert.False(Module1.Equals((object)null));
            Assert.False(Module8.Equals((object)null));
            Assert.True(Module1.Equals((object)Module1));
            Assert.False(Module1.Equals((object)Module3));
            Assert.False(Module1.Equals(new object()));

            Assert.Equal(Module1.GetHashCode(), Module2.GetHashCode());
            Assert.NotEqual(Module1.GetHashCode(), Module3.GetHashCode());

            Assert.Equal(Config1, Config1);
            Assert.Equal(Config1, Config3);
            Assert.NotEqual(Config1, Config2);
            Assert.True(Config1.Equals((object)Config1));
            Assert.False(Config1.Equals(null));
        }

        [Fact]
        [Unit]
        public void TestDeserialize()
        {
            string validJson = "{\"Name\":\"<module_name>\",\"Version\":\"<semantic_version_number>\",\"Type\":\"docker\",\"Status\":\"running\",\"Config\":{\"Image\":\"image1\"}}";
            string validJsonAllLower = "{\"name\":\"<module_name>\",\"version\":\"<semantic_version_number>\",\"type\":\"docker\",\"status\":\"running\",\"config\":{\"image\":\"image1\"}}";
            string validJsonAllCap = "{\"NAME\":\"<module_name>\",\"VERSION\":\"<semantic_version_number>\",\"TYPE\":\"docker\",\"STATUS\":\"RUNNING\",\"CONFIG\":{\"IMAGE\":\"image1\"}}";

            string noNameson = "{\"Version\":\"<semantic_version_number>\",\"Type\":\"docker\",\"Status\":\"running\",\"Config\":{\"Image\":\"<docker_image_name>\"}}";
            string noVersionJson = "{\"Name\":\"<module_name>\",\"Type\":\"docker\",\"Status\":\"running\",\"Config\":{\"Image\":\"<docker_image_name>\"}}";
            string noTypeJson = "{\"Name\":\"<module_name>\",\"Version\":\"<semantic_version_number>\",\"Status\":\"running\",\"Config\":{\"Image\":\"<docker_image_name>\"}}";
            string noStatusJson = "{\"Name\":\"<module_name>\",\"Version\":\"<semantic_version_number>\",\"Type\":\"docker\",\"Config\":{\"Image\":\"<docker_image_name>\"}}";
            string noConfigJson = "{\"Name\":\"<module_name>\",\"Version\":\"<semantic_version_number>\",\"Type\":\"docker\",\"Status\":\"running\"}";
            string noConfigImageJson = "{\"Name\":\"<module_name>\",\"Version\":\"<semantic_version_number>\",\"Type\":\"docker\",\"Status\":\"running\",\"Config\":{}}";
            string validJsonStatusStopped = "{\"Name\":\"<module_name>\",\"Version\":\"<semantic_version_number>\",\"Type\":\"docker\",\"Status\":\"stopped\",\"Config\":{\"Image\":\"<docker_image_name>\"}}";
            string validJsonStatusUnknown = "{\"Name\":\"<module_name>\",\"Version\":\"<semantic_version_number>\",\"Type\":\"docker\",\"Status\":\"Unknown\",\"Config\":{\"Image\":\"<docker_image_name>\"}}";
            string invalidJsonStatus = "{\"Name\":\"<module_name>\",\"Version\":\"<semantic_version_number>\",\"Type\":\"docker\",\"Status\":\"<bad_status>\",\"Config\":{\"Image\":\"<docker_image_name>\"}}";


            ModuleSerde myModuleSerde = ModuleSerde.Instance;

            var myModule1 = myModuleSerde.Deserialize<TestModule>(validJson);
            var myModule2 = myModuleSerde.Deserialize<TestModule>(validJsonAllLower);
            var myModule3 = myModuleSerde.Deserialize<TestModule>(validJsonAllCap);

            Assert.True(ValidJsonModule.Equals(myModule1));
            Assert.True(ValidJsonModule.Equals(myModule2));
            Assert.True(ValidJsonModule.Equals(myModule3));

            Assert.Equal(ModuleStatus.Stopped, myModuleSerde.Deserialize<TestModule>(validJsonStatusStopped).Status);
            Assert.Equal(ModuleStatus.Unknown, myModuleSerde.Deserialize<TestModule>(validJsonStatusUnknown).Status);

            Assert.Throws<JsonSerializationException>(() => myModuleSerde.Deserialize<TestModule>(noStatusJson));
            Assert.Throws<JsonSerializationException>(() => myModuleSerde.Deserialize<TestModule>(noNameson));
            Assert.Throws<JsonSerializationException>(() => myModuleSerde.Deserialize<TestModule>(noVersionJson));
            Assert.Throws<JsonSerializationException>(() => myModuleSerde.Deserialize<TestModule>(noTypeJson));
            Assert.Throws<JsonSerializationException>(() => myModuleSerde.Deserialize<TestModule>(noConfigJson));
            Assert.Throws<JsonSerializationException>(() => myModuleSerde.Deserialize<TestModule>(noConfigImageJson));
            Assert.Throws<JsonSerializationException>(() => myModuleSerde.Deserialize<TestModule>(invalidJsonStatus));
        }

        [Fact]
        [Unit]
        public void ModuleDeserializeMustSpecifyClass()
        {
            string validJson = "{\"Name\":\"<module_name>\",\"Version\":\"<semantic_version_number>\",\"Type\":\"docker\",\"Status\":\"running\",\"Config\":{\"Image\":\"image1\"}}";

            ModuleSerde myModuleSerde = ModuleSerde.Instance;

            Assert.ThrowsAny<JsonException>(() => myModuleSerde.Deserialize(validJson));
        }

        [Fact]
        [Unit]
        public void TestSerialize()
        {
            ModuleSerde myModuleSerde = ModuleSerde.Instance;
            string jsonFromTestModule = myModuleSerde.Serialize(Module8);
            var myModule = myModuleSerde.Deserialize<TestModule>(jsonFromTestModule);

            string jsonFromIModule = myModuleSerde.Serialize(Module1);
            Assert.True(Module8.Equals(myModule));
            Assert.Equal(serializedModule, jsonFromTestModule);
            Assert.Equal(serializedModule, jsonFromIModule);
        }
    }
}