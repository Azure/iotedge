// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core.Test
{
    using System;
    using System.Diagnostics.CodeAnalysis;
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

        [Fact]
        public void TestConstructor()
        {
            Assert.Throws<ArgumentNullException>(() => new TestModule(null, "version1", "type1", ModuleStatus.Running, Config1));
            Assert.Throws<ArgumentNullException>(() => new TestModule("mod1", null, "type1", ModuleStatus.Running, Config1));
            Assert.Throws<ArgumentNullException>(() => new TestModule("mod1", "version1", null, ModuleStatus.Running, Config1));
            Assert.Throws<ArgumentNullException>(() => new TestModule("mod1", "version1", "type1", ModuleStatus.Running, null));
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
    }
}