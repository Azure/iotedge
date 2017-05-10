// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Test
{
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    public class DiffTest
    {
        static readonly TestConfig Config1 = new TestConfig("image1");
        static readonly TestConfig Config2 = new TestConfig("image2");
        static readonly IModule Module1 = new TestModule("mod1", "version1", "type1", ModuleStatus.Running, Config1);
        static readonly IModule Module1A = new TestModule("mod1", "version1", "type1", ModuleStatus.Running, Config1);
        static readonly IModule Module2 = new TestModule("mod2", "version2", "type2", ModuleStatus.Running, Config2);

        [Fact]
        [Unit]
        public void TestEquals()
        {
            
            Diff nonEmptyUpdated = Diff.Create(Module1);
            Diff nonEmptyRemoved = new Diff(ImmutableList<IModule>.Empty, new List<string>{"module2"});
            Diff alsoNonEmptyDiff = nonEmptyUpdated;

            Assert.False(nonEmptyUpdated.Equals(null));
            Assert.True(nonEmptyUpdated.Equals(alsoNonEmptyDiff));
            Assert.False(nonEmptyUpdated.Equals(Config1));

            Assert.False(Diff.Empty.Equals(nonEmptyUpdated));
            Assert.False(Diff.Empty.Equals(nonEmptyRemoved));
            Assert.False(nonEmptyUpdated.Equals(nonEmptyRemoved));

            Assert.Equal(Module1,Module1A);
            Assert.True(nonEmptyUpdated.Equals(Diff.Create(Module1A)));
        }

        [Fact]
        [Unit]
        public void TestDiffUnordered()
        {
            Diff diff1 = new Diff(new List<IModule>{ Module1, Module2}, new List<string> {"mod3", "mod4"});
            Diff diff2 = new Diff(new List<IModule>{ Module2, Module1}, new List<string> {"mod4", "mod3"});

            Assert.Equal(diff1, diff2);
        }

        [Fact]
        [Unit]
        public void TestDiffHash()
        {
            Diff diff1 = new Diff(new List<IModule>{ Module1, Module2}, new List<string> {"mod3", "mod4"});
            Diff diff2 = new Diff(new List<IModule> { Module2, Module1 }, new List<string> { "mod4", "mod3" });
            Diff diff3 = new Diff(new List<IModule> { Module1A, Module2 }, new List<string> { "mod3", "mod4" });
            Diff diff4 = new Diff(new List<IModule> { Module1 }, new List<string> { "mod3" });
            Diff diff5 = new Diff(new List<IModule> { Module2 }, new List<string> { "mod3" });
            Diff diff6 = new Diff(new List<IModule> { Module1 }, new List<string> { "mod3" });
            Diff diff7 = new Diff(new List<IModule> { Module1 }, new List<string> { "mod4" });

            int hash1 = diff1.GetHashCode();
            int hash2 = diff2.GetHashCode();
            int hash3 = diff3.GetHashCode();
            int hash4 = diff4.GetHashCode();
            int hash5 = diff5.GetHashCode();
            int hash6 = diff6.GetHashCode();
            int hash7 = diff7.GetHashCode();

            Assert.Equal(hash1,hash2);
            Assert.Equal(hash1,hash3);
            Assert.NotEqual(hash4,hash5);
            Assert.NotEqual(hash6,hash7);
        }
    }
}