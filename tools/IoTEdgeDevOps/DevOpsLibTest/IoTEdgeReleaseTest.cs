// Copyright (c) Microsoft. All rights reserved.
namespace DevOpsLibTest
{
    using System;
    using System.Collections.Generic;
    using DevOpsLib;
    using DevOpsLib.VstsModels;
    using NUnit.Framework;

    [TestFixture]
    public class IoTEdgeReleaseTest
    {
        [Test]
        public void TestConstructorWithInvalidId([Values(-1, 0)] int id)
        {
            ArgumentOutOfRangeException ex = Assert.Throws<ArgumentOutOfRangeException>(
                () =>
                    new IoTEdgeRelease(
                        id,
                        343406,
                        "test-release",
                        new Uri("http://abc.com/test/uri"),
                        new HashSet<IoTEdgeReleaseEnvironment>
                        {
                            new IoTEdgeReleaseEnvironment(3423, 8790, VstsEnvironmentStatus.NotStarted),
                            new IoTEdgeReleaseEnvironment(784, 23903, VstsEnvironmentStatus.InProgress)
                        }));
            Assert.True(ex.Message.StartsWith("Cannot be less than or equal to zero."));
            Assert.AreEqual("id", ex.ParamName);
        }

        [Test]
        public void TestConstructorWithInvalidDefinitionId([Values(-1, 0)] int definitionId)
        {
            ArgumentOutOfRangeException ex = Assert.Throws<ArgumentOutOfRangeException>(
                () =>
                    new IoTEdgeRelease(
                        3782954,
                        definitionId,
                        "test-release",
                        new Uri("http://abc.com/test/uri"),
                        new HashSet<IoTEdgeReleaseEnvironment>
                        {
                            new IoTEdgeReleaseEnvironment(3423, 8790, VstsEnvironmentStatus.NotStarted),
                            new IoTEdgeReleaseEnvironment(784, 23903, VstsEnvironmentStatus.InProgress)
                        }));
            Assert.True(ex.Message.StartsWith("Cannot be less than or equal to zero."));
            Assert.AreEqual("definitionId", ex.ParamName);
        }

        [Test]
        public void TestConstructorWithInvalidName([Values(null, "")] string name)
        {
            ArgumentException ex = Assert.Throws<ArgumentException>(
                () =>
                    new IoTEdgeRelease(
                        3782954,
                        7348,
                        name,
                        new Uri("http://abc.com/test/uri"),
                        new HashSet<IoTEdgeReleaseEnvironment>
                        {
                            new IoTEdgeReleaseEnvironment(3423, 8790, VstsEnvironmentStatus.NotStarted),
                            new IoTEdgeReleaseEnvironment(784, 23903, VstsEnvironmentStatus.InProgress)
                        }));
            Assert.True(ex.Message.StartsWith("Cannot be null or white space."));
            Assert.AreEqual("name", ex.ParamName);
        }

        [Test]
        public void TestConstructorWithNullWebUri()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(
                () =>
                    new IoTEdgeRelease(
                        3782954,
                        7348,
                        "Relase-2378",
                        null,
                        new HashSet<IoTEdgeReleaseEnvironment>
                        {
                            new IoTEdgeReleaseEnvironment(3423, 8790, VstsEnvironmentStatus.NotStarted),
                            new IoTEdgeReleaseEnvironment(784, 23903, VstsEnvironmentStatus.InProgress)
                        }));
            Assert.True(ex.Message.StartsWith("Cannot be null."));
            Assert.AreEqual("webUri", ex.ParamName);
        }

        [Test]
        public void TestConstructorWithNullEnvironments()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(
                () =>
                    new IoTEdgeRelease(
                        3782954,
                        7348,
                        "Relase-2378",
                        new Uri("http://abc.com/test/uri"),
                        null));
            Assert.True(ex.Message.StartsWith("Cannot be null."));
            Assert.AreEqual("environments", ex.ParamName);
        }

        [Test]
        public void TestProperties()
        {
            var release = new IoTEdgeRelease(
                123213,
                343406,
                "test-release",
                new Uri("http://abc.com/test/uri"),
                new HashSet<IoTEdgeReleaseEnvironment>
                {
                    new IoTEdgeReleaseEnvironment(3423, 8790, VstsEnvironmentStatus.NotStarted),
                    new IoTEdgeReleaseEnvironment(784, 23903, VstsEnvironmentStatus.InProgress)
                });

            Assert.AreEqual(123213, release.Id);
            Assert.AreEqual(343406, release.DefinitionId);
            Assert.AreEqual("test-release", release.Name);
            Assert.AreEqual("http://abc.com/test/uri", release.WebUri.AbsoluteUri);
            Assert.AreEqual(2, release.NumberOfEnvironments);
            var env1 = release.GetEnvironment(8790);
            Assert.AreEqual(3423, env1.Id);
            Assert.AreEqual(VstsEnvironmentStatus.NotStarted, env1.Status);
            var env2 = release.GetEnvironment(23903);
            Assert.AreEqual(784, env2.Id);
            Assert.AreEqual(VstsEnvironmentStatus.InProgress, env2.Status);
        }

        [Test]
        public void TestEquals()
        {
            var release1 = new IoTEdgeRelease(
                123213,
                343406,
                "test-release",
                new Uri("http://abc.com/test/uri"),
                new HashSet<IoTEdgeReleaseEnvironment>
                {
                    new IoTEdgeReleaseEnvironment(3423, 8790, VstsEnvironmentStatus.NotStarted),
                    new IoTEdgeReleaseEnvironment(784, 23903, VstsEnvironmentStatus.InProgress)
                });

            var release2 = new IoTEdgeRelease(
                123213,
                343406,
                "test-release",
                new Uri("http://abc.com/test/uri"),
                new HashSet<IoTEdgeReleaseEnvironment>
                {
                    new IoTEdgeReleaseEnvironment(3423, 8790, VstsEnvironmentStatus.NotStarted),
                    new IoTEdgeReleaseEnvironment(784, 23903, VstsEnvironmentStatus.InProgress)
                });

            var release3 = new IoTEdgeRelease(
                2343,
                343406,
                "test-release",
                new Uri("http://abc.com/test/uri"),
                new HashSet<IoTEdgeReleaseEnvironment>
                {
                    new IoTEdgeReleaseEnvironment(3423, 8790, VstsEnvironmentStatus.NotStarted),
                    new IoTEdgeReleaseEnvironment(784, 23903, VstsEnvironmentStatus.InProgress)
                });

            var release4 = new IoTEdgeRelease(
                123213,
                788,
                "test-release",
                new Uri("http://abc.com/test/uri"),
                new HashSet<IoTEdgeReleaseEnvironment>
                {
                    new IoTEdgeReleaseEnvironment(3423, 8790, VstsEnvironmentStatus.NotStarted),
                    new IoTEdgeReleaseEnvironment(784, 23903, VstsEnvironmentStatus.InProgress)
                });

            var release5 = new IoTEdgeRelease(
                123213,
                343406,
                "test-release342",
                new Uri("http://abc.com/test/uri"),
                new HashSet<IoTEdgeReleaseEnvironment>
                {
                    new IoTEdgeReleaseEnvironment(3423, 8790, VstsEnvironmentStatus.NotStarted),
                    new IoTEdgeReleaseEnvironment(784, 23903, VstsEnvironmentStatus.InProgress)
                });

            var release6 = new IoTEdgeRelease(
                123213,
                343406,
                "test-release",
                new Uri("http://abc.com/test/uri/123123"),
                new HashSet<IoTEdgeReleaseEnvironment>
                {
                    new IoTEdgeReleaseEnvironment(3423, 8790, VstsEnvironmentStatus.NotStarted),
                    new IoTEdgeReleaseEnvironment(784, 23903, VstsEnvironmentStatus.InProgress)
                });

            var release7 = new IoTEdgeRelease(
                123213,
                343406,
                "test-release",
                new Uri("http://abc.com/test/uri/123123"),
                new HashSet<IoTEdgeReleaseEnvironment>
                {
                    new IoTEdgeReleaseEnvironment(3423, 8790, VstsEnvironmentStatus.NotStarted)
                });

            Assert.False(release1.Equals(null));
            Assert.True(release1.Equals(release1));
            Assert.True(release1.Equals(release2));

            Assert.False(release1.Equals((object) null));
            Assert.True(release1.Equals((object) release1));
            Assert.True(release1.Equals((object) release2));
            Assert.False(release1.Equals(new object()));

            Assert.False(release1.Equals(release3));
            Assert.False(release1.Equals(release4));
            Assert.False(release1.Equals(release5));
            Assert.False(release1.Equals(release6));
            Assert.False(release1.Equals(release7));
        }

        [Test]
        public void TestGetHashCode()
        {
            var release = new IoTEdgeRelease(
                123213,
                343406,
                "test-release",
                new Uri("http://abc.com/test/uri"),
                new HashSet<IoTEdgeReleaseEnvironment>
                {
                    new IoTEdgeReleaseEnvironment(3423, 8790, VstsEnvironmentStatus.NotStarted),
                    new IoTEdgeReleaseEnvironment(784, 23903, VstsEnvironmentStatus.InProgress)
                });

            Assert.AreEqual(123213, release.GetHashCode());
        }

        [Test]
        public void TestGetEnvironment()
        {
            var release = new IoTEdgeRelease(
                123213,
                343406,
                "test-release",
                new Uri("http://abc.com/test/uri"),
                new HashSet<IoTEdgeReleaseEnvironment>
                {
                    new IoTEdgeReleaseEnvironment(3423, 8790, VstsEnvironmentStatus.NotStarted),
                    new IoTEdgeReleaseEnvironment(784, 23903, VstsEnvironmentStatus.InProgress),
                    new IoTEdgeReleaseEnvironment(38934, 23903, VstsEnvironmentStatus.Succeeded)
                });

            Assert.AreEqual(3423, release.GetEnvironment(8790).Id);
            Assert.AreEqual(784, release.GetEnvironment(23903).Id);
            Assert.AreEqual(IoTEdgeReleaseEnvironment.CreateEnvironmentWithNoResult(9999), release.GetEnvironment(9999));
        }
    }
}
