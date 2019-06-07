// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Test
{
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;

    public class DiffTest
    {
        static readonly IDictionary<string, EnvVal> EnvVars = new Dictionary<string, EnvVal>();
        static readonly TestConfig Config1 = new TestConfig("image1");
        static readonly TestConfig Config2 = new TestConfig("image2");
        static readonly IModule Module1 = new TestModule("mod1", "version1", "test", ModuleStatus.Running, Config1, RestartPolicy.OnUnhealthy, ImagePullPolicy.OnCreate, new ConfigurationInfo("1"), EnvVars);
        static readonly IModule Module1A = new TestModule("mod1", "version1", "test", ModuleStatus.Running, Config1, RestartPolicy.OnUnhealthy, ImagePullPolicy.OnCreate, new ConfigurationInfo("1"), EnvVars);
        static readonly IModule Module2 = new TestModule("mod2", "version2", "type2", ModuleStatus.Running, Config2, RestartPolicy.OnUnhealthy, ImagePullPolicy.OnCreate, new ConfigurationInfo("1"), EnvVars);
        static readonly IModule Module2A = new TestModule("mod2", "version2", "type2", ModuleStatus.Stopped, Config2, RestartPolicy.OnUnhealthy, ImagePullPolicy.OnCreate, new ConfigurationInfo("1"), EnvVars);
        static readonly IModule Module2B = new TestModule("mod2", "version2", "type2", ModuleStatus.Stopped, Config2, RestartPolicy.OnUnhealthy, ImagePullPolicy.OnCreate, new ConfigurationInfo("1"), EnvVars);
        static readonly IModule Module3 = new TestModule("mod3", "version3", "type3", ModuleStatus.Stopped, Config2, RestartPolicy.OnUnhealthy, ImagePullPolicy.OnCreate, new ConfigurationInfo("1"), EnvVars);

        [Fact]
        [Unit]
        public void TestEquals()
        {
            Diff nonEmptyUpdated = new Diff.Builder().WithAdded(Module1).Build();
            var nonEmptyRemoved = new Diff.Builder().WithRemoved("module2").Build();
            Diff alsoNonEmptyDiff = nonEmptyUpdated;
            object nonEmptyUpdatedObjectSameReference = nonEmptyUpdated;

            Assert.False(nonEmptyUpdated.Equals(null));
            Assert.True(nonEmptyUpdated.Equals(alsoNonEmptyDiff));
            Assert.False(nonEmptyUpdated.Equals(new object()));
            Assert.True(nonEmptyUpdated.Equals(nonEmptyUpdatedObjectSameReference));

            Assert.False(Diff.Empty.Equals(nonEmptyUpdated));
            Assert.False(Diff.Empty.Equals(nonEmptyRemoved));
            Assert.False(nonEmptyUpdated.Equals(nonEmptyRemoved));

            Assert.Equal(Module1, Module1A);
            Assert.True(nonEmptyUpdated.Equals(new Diff.Builder().WithAdded(Module1A).Build()));
        }

        [Fact]
        [Unit]
        public void TestDiffUnordered()
        {
            Diff diff1 = new Diff.Builder()
                .WithAdded(Module1, Module2)
                .WithRemoved("m3", "m4")
                .WithUpdated(Module2A, Module3)
                .Build();

            Diff diff2 = new Diff.Builder()
                .WithAdded(Module2, Module1)
                .WithRemoved("m4", "m3")
                .WithUpdated(Module3, Module2A)
                .Build();

            Assert.Equal(diff1, diff2);
        }

        [Fact]
        [Unit]
        public void TestDiffHash()
        {
            var diff1 = new Diff.Builder()
                .WithAdded(Module1, Module2)
                .WithRemoved("mod3", "mod4")
                .Build();

            var diff2 = new Diff.Builder()
                .WithAdded(Module2, Module1)
                .WithRemoved("mod4", "mod3")
                .Build();

            var diff3 = new Diff.Builder()
                .WithAdded(Module1A, Module2)
                .WithRemoved("mod3", "mod4")
                .Build();

            var diff4 = new Diff.Builder()
                .WithAdded(Module1)
                .WithRemoved("mod3")
                .Build();

            var diff5 = new Diff.Builder()
                .WithAdded(Module2)
                .WithRemoved("mod3")
                .Build();

            var diff6 = new Diff.Builder()
                .WithAdded(Module1)
                .WithRemoved("mod3")
                .Build();

            var diff7 = new Diff.Builder()
                .WithAdded(Module1)
                .WithRemoved("mod4")
                .Build();

            int hash1 = diff1.GetHashCode();
            int hash2 = diff2.GetHashCode();
            int hash3 = diff3.GetHashCode();
            int hash4 = diff4.GetHashCode();
            int hash5 = diff5.GetHashCode();
            int hash6 = diff6.GetHashCode();
            int hash7 = diff7.GetHashCode();

            Assert.Equal(hash1, hash2);
            Assert.Equal(hash1, hash3);
            Assert.NotEqual(hash4, hash5);
            Assert.NotEqual(hash6, hash7);
        }

        [Fact]
        [Unit]
        public void TestDiffEmpty()
        {
            // arrange
            Diff diff1 = new Diff.Builder().Build();

            // act
            // assert
            Assert.True(Diff.Empty.Equals(diff1));
            Assert.True(diff1.IsEmpty);

            Diff diff2 = new Diff.Builder().WithAdded(Mock.Of<IModule>()).Build();
            Assert.False(diff2.IsEmpty);

            Diff diff3 = new Diff.Builder().WithDesiredStatusUpdated(Mock.Of<IModule>()).Build();
            Assert.False(diff2.IsEmpty);

            Diff diff4 = new Diff.Builder().WithUpdated(Mock.Of<IModule>()).Build();
            Assert.False(diff2.IsEmpty);

            Diff diff5 = new Diff.Builder().WithRemoved("mod1").Build();
            Assert.False(diff2.IsEmpty);
        }
    }
}
