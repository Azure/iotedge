// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.Test
{
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment.Diff;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    public class SetTest
    {
        [Theory]
        [Unit]
        [MemberData(nameof(DiffTestSet.TestDataForDefaultComparison), MemberType = typeof(DiffTestSet))]
        public void ReturnsExpectedDiffResult(Set<Module> either, Set<Module> other, Diff<Module> expected)
        {
            Diff<Module> actual = other.Diff(either);
            Assert.Equal(expected, actual);
        }

        [Theory]
        [Unit]
        [MemberData(nameof(DiffTestSet.TestDataForByNameComparison), MemberType = typeof(DiffTestSet))]
        public void ReturnsExpectedDiffResultWithComparer(Set<Module> either, Set<Module> other, Diff<Module> expected)
        {
            IEqualityComparer<Module> comparer = new ByNameComparer();
            Diff<Module> actual = other.Diff(either, comparer);
            Assert.Equal(expected, actual);
        }

        public class Module
        {
            public Module(string name)
            {
                this.Name = name;
            }

            public string Name { get; }
        }

        sealed class ByNameComparer : IEqualityComparer<Module>
        {
            public bool Equals(Module x, Module y)
            {
                if (ReferenceEquals(x, y))
                {
                    return true;
                }

                if (ReferenceEquals(x, null))
                {
                    return false;
                }

                if (ReferenceEquals(null, y))
                {
                    return false;
                }

                return x.Name == y.Name;
            }

            public int GetHashCode(Module obj) => obj.Name.GetHashCode();
        }

        static readonly Module Module1 = new Module("mod1");
        static readonly Module Module2 = new Module("mod2");
        static readonly Module Module3 = new Module("mod3");
        static readonly Module Module4 = new Module("mod4");
        static readonly Module Module5 = new Module("mod5");
        static readonly Module Module2Duplicate = new Module("mod2");

        static class DiffTestSet
        {
            static Set<Module> Create(params Module[] modules)
                => new Set<Module>(modules.ToDictionary(module => module.Name));

            public static IEnumerable<object[]> TestDataForDefaultComparison => new List<object[]>
            {
                // adding modules
                new object[]
                {
                    Set<Module>.Empty,
                    Create(Module1, Module2),
                    new Diff<Module>.Builder()
                        .WithAdded(Module1, Module2)
                        .Build()
                },

                // removing modules
                new object[]
                {
                    Create(Module1, Module2),
                    Set<Module>.Empty,
                    new Diff<Module>.Builder()
                        .WithRemoved("mod1", "mod2")
                        .Build()
                },

                // change a module
                new object[]
                {
                    Create(Module2, Module1),
                    Create(Module4, Module2),
                    new Diff<Module>.Builder()
                        .WithAdded(Module4)
                        .WithRemoved("mod1")
                        .Build()
                },

                // no changes
                new object[]
                {
                    Create(Module5, Module3, Module2),
                    Create(Module2, Module3, Module5),
                    Diff<Module>.Empty
                },

                // changes because of different objects
                new object[]
                {
                    Create(Module1, Module2),
                    Create(Module2Duplicate, Module1),
                    new Diff<Module>.Builder()
                        .WithUpdated(new Update<Module>(Module2, Module2Duplicate))
                        .Build()
                }
            };

            public static IEnumerable<object[]> TestDataForByNameComparison => new List<object[]>
            {
                // adding modules
                new object[]
                {
                    Set<Module>.Empty,
                    Create(Module1, Module2),
                    new Diff<Module>.Builder()
                        .WithAdded(Module1, Module2)
                        .Build()
                },

                // removing modules
                new object[]
                {
                    Create(Module1, Module2),
                    Set<Module>.Empty,
                    new Diff<Module>.Builder()
                        .WithRemoved("mod1", "mod2")
                        .Build()
                },

                // change a module
                new object[]
                {
                    Create(Module2, Module1),
                    Create(Module4, Module2),
                    new Diff<Module>.Builder()
                        .WithAdded(Module4)
                        .WithRemoved("mod1")
                        .Build()
                },

                // no changes
                new object[]
                {
                    Create(Module5, Module3, Module2),
                    Create(Module2, Module3, Module5),
                    Diff<Module>.Empty
                },

                // no change because of identical names
                new object[]
                {
                    Create(Module1, Module2),
                    Create(Module2Duplicate, Module1),
                    Diff<Module>.Empty
                }
            };
        }
    }
}
