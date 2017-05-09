// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Docker.Test
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Core.Serde;
    using Microsoft.Azure.Devices.Edge.Agent.Docker;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Newtonsoft.Json;
    using Xunit;

    [ExcludeFromCodeCoverage]
    public class DockerModuleTest
    {
        static readonly DockerConfig Config1 = new DockerConfig("image1");
        static readonly DockerConfig Config2 = new DockerConfig("image2");

        static readonly IModule Module1 = new DockerModule("mod1", "version1", ModuleStatus.Running, Config1);
        static readonly IModule Module2 = new DockerModule("mod1", "version1", ModuleStatus.Running, Config1);
        static readonly IModule Module3 = new DockerModule("mod3", "version1", ModuleStatus.Running, Config1);
        static readonly IModule Module4 = new DockerModule("mod1", "version2", ModuleStatus.Running, Config1);
        static readonly IModule Module6 = new DockerModule("mod1", "version1", ModuleStatus.Unknown, Config1);
        static readonly IModule Module7 = new DockerModule("mod1", "version1", ModuleStatus.Running, Config2);
        static readonly DockerModule Module8 = new DockerModule("mod1", "version1", ModuleStatus.Running, Config1);
        static readonly DockerModule ValidJsonModule = new DockerModule("<module_name>", "<semantic_version_number>", ModuleStatus.Running, Config1);

        const string SerializedModule = "{\"name\":\"mod1\",\"version\":\"version1\",\"type\":\"docker\",\"status\":\"running\",\"config\":{\"image\":\"image1\"}}";

        [Fact]
        [Unit]
        public void TestConstructor()
        {
            Assert.Throws<ArgumentNullException>(() => new DockerModule(null, "version1", ModuleStatus.Running, Config1));
            Assert.Throws<ArgumentNullException>(() => new DockerModule("mod1", null, ModuleStatus.Running, Config1));
            Assert.Throws<ArgumentNullException>(() => new DockerModule("mod1", "version1", ModuleStatus.Running, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DockerModule("mod1", "version1", (ModuleStatus)int.MaxValue, Config1));
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
            Assert.True(Module1.Equals((object)Module2));
            Assert.False(Module1.Equals((object)Module3));
            Assert.False(Module1.Equals(new object()));
            Assert.True(Module1.Equals((IModule<DockerConfig>)Module1));

            Assert.Equal(Module1.GetHashCode(), Module2.GetHashCode());
            Assert.NotEqual(Module1.GetHashCode(), Module3.GetHashCode());
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


            ModuleSerde myModuleSerde = ModuleSerde.Instance;

            var myModule1 = myModuleSerde.Deserialize<DockerModule>(validJson);
            var myModule2 = myModuleSerde.Deserialize<DockerModule>(validJsonAllLower);
            var myModule3 = myModuleSerde.Deserialize<DockerModule>(validJsonAllCap);

            Assert.True(ValidJsonModule.Equals(myModule1));
            Assert.True(ValidJsonModule.Equals(myModule2));
            Assert.True(ValidJsonModule.Equals(myModule3));

            Assert.Equal(ModuleStatus.Stopped, myModuleSerde.Deserialize<DockerModule>(validJsonStatusStopped).Status);
            Assert.Equal(ModuleStatus.Unknown, myModuleSerde.Deserialize<DockerModule>(validJsonStatusUnknown).Status);

            Assert.Throws<JsonSerializationException>(() => myModuleSerde.Deserialize<DockerModule>(noStatusJson));
            Assert.Throws<JsonSerializationException>(() => myModuleSerde.Deserialize<DockerModule>(noNameson));
            Assert.Throws<JsonSerializationException>(() => myModuleSerde.Deserialize<DockerModule>(noVersionJson));
            Assert.Throws<JsonSerializationException>(() => myModuleSerde.Deserialize<DockerModule>(noTypeJson));
            Assert.Throws<JsonSerializationException>(() => myModuleSerde.Deserialize<DockerModule>(noConfigJson));
            Assert.Throws<JsonSerializationException>(() => myModuleSerde.Deserialize<DockerModule>(noConfigImageJson));
        }

        [Fact]
        [Unit]
        public void TestSerialize()
        {
            ModuleSerde myModuleSerde = ModuleSerde.Instance;
            string jsonFromDockerModule = myModuleSerde.Serialize(Module8);
            IModule myModule = myModuleSerde.Deserialize<DockerModule>(jsonFromDockerModule);

            string jsonFromIModule = myModuleSerde.Serialize(Module1);
            Assert.True(Module8.Equals(myModule));
            Assert.Equal(SerializedModule, jsonFromDockerModule);
            Assert.Equal(SerializedModule, jsonFromIModule);
        }
    }
}
