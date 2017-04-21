// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Docker.Test
{
    using System;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Docker;
    using Xunit;

    public class DockerModuleTest
    {
        static readonly DockerConfig Config1 = new DockerConfig("image1");
        static readonly DockerConfig Config2 = new DockerConfig("image2");

        static readonly IModule Module1 = new DockerModule("mod1", "version1", "type1", ModuleStatus.Running, Config1);
        static readonly IModule Module2 = new DockerModule("mod1", "version1", "type1", ModuleStatus.Running, Config1);
        static readonly IModule Module3 = new DockerModule("mod3", "version1", "type1", ModuleStatus.Running, Config1);
        static readonly IModule Module4 = new DockerModule("mod1", "version2", "type1", ModuleStatus.Running, Config1);
        static readonly IModule Module5 = new DockerModule("mod1", "version1", "type2", ModuleStatus.Running, Config1);
        static readonly IModule Module6 = new DockerModule("mod1", "version1", "type1", ModuleStatus.Unknown, Config1);
        static readonly IModule Module7 = new DockerModule("mod1", "version1", "type1", ModuleStatus.Running, Config2);
        static readonly DockerModule Module8 = new DockerModule("mod1", "version1", "type1", ModuleStatus.Running, Config1);

        [Fact]
        public void TestConstructor()
        {
            Assert.Throws<ArgumentNullException>(() => new DockerModule(null, "version1", "type1", ModuleStatus.Running, Config1));
            Assert.Throws<ArgumentNullException>(() => new DockerModule("mod1", null, "type1", ModuleStatus.Running, Config1));
            Assert.Throws<ArgumentNullException>(() => new DockerModule("mod1", "version1", null, ModuleStatus.Running, Config1));
            Assert.Throws<ArgumentNullException>(() => new DockerModule("mod1", "version1", "type1", ModuleStatus.Running, null));
        }

        [Fact]
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
            Assert.True(Module1.Equals((object)Module2));
            Assert.False(Module1.Equals((object)Module3));
            Assert.False(Module1.Equals(new object()));
            Assert.True(Module1.Equals((IModule<DockerConfig>)Module1));

            Assert.Equal(Module1.GetHashCode(), Module2.GetHashCode());
            Assert.NotEqual(Module1.GetHashCode(), Module3.GetHashCode());
        }
    }
}