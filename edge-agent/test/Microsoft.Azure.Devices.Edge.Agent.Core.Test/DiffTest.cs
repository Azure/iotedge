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

    public class DiffTest
    {
        static readonly TestConfig Config1 = new TestConfig("image1");
        static readonly TestConfig Config2 = new TestConfig("image2");
        static readonly IModule Module1 = new TestModule("mod1", "version1", "test", ModuleStatus.Running, Config1, RestartPolicy.OnUnhealthy, new ConfigurationInfo("1"));
        static readonly IModule Module1A = new TestModule("mod1", "version1", "test", ModuleStatus.Running, Config1, RestartPolicy.OnUnhealthy, new ConfigurationInfo("1"));
        static readonly IModule Module2 = new TestModule("mod2", "version2", "type2", ModuleStatus.Running, Config2, RestartPolicy.OnUnhealthy, new ConfigurationInfo("1"));

        [Fact]
        [Unit]
        public void TestEquals()
        {
            Diff nonEmptyUpdated = Diff.Create(Module1);
            var nonEmptyRemoved = new Diff(ImmutableList<IModule>.Empty, new List<string>{"module2"});
            Diff alsoNonEmptyDiff = nonEmptyUpdated;
            object nonEmptyUpdatedObjectSameReference = nonEmptyUpdated;

            Assert.False(nonEmptyUpdated.Equals(null));
            Assert.True(nonEmptyUpdated.Equals(alsoNonEmptyDiff));
            Assert.False(nonEmptyUpdated.Equals(new object()));
            Assert.True(nonEmptyUpdated.Equals(nonEmptyUpdatedObjectSameReference));

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
            var diff1 = new Diff(new List<IModule>{ Module1, Module2}, new List<string> {"mod3", "mod4"});
            var diff2 = new Diff(new List<IModule>{ Module2, Module1}, new List<string> {"mod4", "mod3"});

            Assert.Equal(diff1, diff2);
        }

        [Fact]
        [Unit]
        public void TestDiffHash()
        {
            var diff1 = new Diff(new List<IModule>{ Module1, Module2}, new List<string> {"mod3", "mod4"});
            var diff2 = new Diff(new List<IModule> { Module2, Module1 }, new List<string> { "mod4", "mod3" });
            var diff3 = new Diff(new List<IModule> { Module1A, Module2 }, new List<string> { "mod3", "mod4" });
            var diff4 = new Diff(new List<IModule> { Module1 }, new List<string> { "mod3" });
            var diff5 = new Diff(new List<IModule> { Module2 }, new List<string> { "mod3" });
            var diff6 = new Diff(new List<IModule> { Module1 }, new List<string> { "mod3" });
            var diff7 = new Diff(new List<IModule> { Module1 }, new List<string> { "mod4" });

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

        [Fact]
        [Unit]
        public void TestDiffEmpty()
        {
            //arrange
            var diff1 = new Diff(ImmutableList<IModule>.Empty, ImmutableList<string>.Empty);
            //act
            //assert
            Assert.True(Diff.Empty.Equals(diff1));
            Assert.True(diff1.IsEmpty);
        }

        [Fact]
        [Unit]
        public void TestDiffSerialize()
        {
            //arrange
            var serializerInputTable = new Dictionary<string, Type>() { { "test", typeof(TestModule) } };
            var diffSerde = new DiffSerde(serializerInputTable);
            Diff nonEmptyUpdated = Diff.Create(Module1);

            //act
            //assert
            Assert.Throws<NotSupportedException>(() => diffSerde.Serialize(nonEmptyUpdated));
        }
        [Fact]
        [Unit]
        public void TestDiffDeserialize()
        {
            //"mod1", "version1", "type1", ModuleStatus.Running, Config1
            //Config1 = new TestConfig("image1");
            //arrange
            Diff nonEmptyUpdated = Diff.Create(Module1);
            string nonEmptyUpdatedJson = "{\"modules\":{\"mod1\":{\"version\":\"version1\",\"type\":\"test\",\"status\":\"running\",\"settings\":{\"image\":\"image1\"},\"restartPolicy\":\"on-unhealthy\",\"configuration\":{\"id\":\"1\",\"version\":\"2\"}}},\"$version\":127}";
            string nonEmptyRemovedJson = "{\"modules\":{\"module2\": null },\"$version\":127}";
            string nonSupportedTypeModuleJson = "{\"modules\":{\"mod1\":{\"version\":\"version1\",\"type\":\"unknown\",\"status\":\"running\",\"settings\":{\"image\":\"image1\"},\"restartPolicy\":\"on-unhealthy\",\"configuration\":{\"id\":\"1\",\"version\":\"2\"}}},\"$version\":127}";
            string noTypeDiffJson = "{\"modules\":{\"mod1\":{\"version\":\"version1\",\"status\":\"running\",\"settings\":{\"image\":\"image1\"},\"restartPolicy\":\"on-unhealthy\",\"configuration\":{\"id\":\"1\",\"version\":\"2\"}}},\"$version\":127}";

            var nonEmptyRemoved = new Diff(ImmutableList<IModule>.Empty, new List<string> { "module2" });

            var serializerInputTable = new Dictionary<string, Type>() { { "test", typeof(TestModule) } };
            var diffSerde = new DiffSerde(serializerInputTable);

            //act
            Diff nonEmptyUpdatedDeserialized = diffSerde.Deserialize(nonEmptyUpdatedJson);
            Diff nonEmptyRemovedDeserialized = diffSerde.Deserialize(nonEmptyRemovedJson);

            //assert
            Assert.Throws<JsonSerializationException>(() => diffSerde.Deserialize(nonSupportedTypeModuleJson));
            Assert.Throws<NotSupportedException>(() => diffSerde.Deserialize<Diff>(nonEmptyUpdatedJson));
            Assert.Throws<JsonSerializationException>(() => diffSerde.Deserialize(noTypeDiffJson));
            Assert.True(nonEmptyUpdatedDeserialized.Equals(nonEmptyUpdated));
            Assert.True(nonEmptyRemovedDeserialized.Equals(nonEmptyRemoved));

        }
    }
}
