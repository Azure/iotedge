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
                        ReleaseDefinitionId.E2ETest,
                        "test-release",
                        "branch1",
                        VstsReleaseStatus.Active,
                        new Uri("http://abc.com/test/uri"),
                        new HashSet<IoTEdgeReleaseEnvironment>
                        {
                            new IoTEdgeReleaseEnvironment(3423, 8790, "Any name", VstsEnvironmentStatus.NotStarted, new HashSet<IoTEdgeReleaseDeployment>()),
                            new IoTEdgeReleaseEnvironment(784, 23903, "Any name", VstsEnvironmentStatus.InProgress, new HashSet<IoTEdgeReleaseDeployment>())
                        }));
            Assert.True(ex.Message.StartsWith("Cannot be less than or equal to zero."));
            Assert.AreEqual("id", ex.ParamName);
        }

        [Test]
        public void TestConstructorWithInvalidName([Values(null, "")] string name)
        {
            ArgumentException ex = Assert.Throws<ArgumentException>(
                () =>
                    new IoTEdgeRelease(
                        3782954,
                        ReleaseDefinitionId.E2ETest,
                        name,
                        "branch1",
                        VstsReleaseStatus.Active,
                        new Uri("http://abc.com/test/uri"),
                        new HashSet<IoTEdgeReleaseEnvironment>
                        {
                            new IoTEdgeReleaseEnvironment(3423, 8790, "Any name", VstsEnvironmentStatus.NotStarted, new HashSet<IoTEdgeReleaseDeployment>()),
                            new IoTEdgeReleaseEnvironment(784, 23903, "Any name", VstsEnvironmentStatus.InProgress, new HashSet<IoTEdgeReleaseDeployment>())
                        }));
            Assert.True(ex.Message.StartsWith("Cannot be null or white space."));
            Assert.AreEqual("name", ex.ParamName);
        }

        [Test]
        public void TestConstructorWithInvalidSourceBranch([Values(null, "")] string sourceBranch)
        {
            ArgumentException ex = Assert.Throws<ArgumentException>(
                () =>
                    new IoTEdgeRelease(
                        3782954,
                        ReleaseDefinitionId.E2ETest,
                        "Relase-2378",
                        sourceBranch,
                        VstsReleaseStatus.Active,
                        new Uri("http://abc.com/test/uri"),
                        new HashSet<IoTEdgeReleaseEnvironment>
                        {
                            new IoTEdgeReleaseEnvironment(3423, 8790, "Any name", VstsEnvironmentStatus.NotStarted, new HashSet<IoTEdgeReleaseDeployment>()),
                            new IoTEdgeReleaseEnvironment(784, 23903, "Any name", VstsEnvironmentStatus.InProgress, new HashSet<IoTEdgeReleaseDeployment>())
                        }));
            Assert.True(ex.Message.StartsWith("Cannot be null or white space."));
            Assert.AreEqual("sourceBranch", ex.ParamName);
        }

        [Test]
        public void TestConstructorWithNullWebUri()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(
                () =>
                    new IoTEdgeRelease(
                        3782954,
                        ReleaseDefinitionId.E2ETest,
                        "Relase-2378",
                        "branch1",
                        VstsReleaseStatus.Active,
                        null,
                        new HashSet<IoTEdgeReleaseEnvironment>
                        {
                            new IoTEdgeReleaseEnvironment(3423, 8790, "Any name", VstsEnvironmentStatus.NotStarted, new HashSet<IoTEdgeReleaseDeployment>()),
                            new IoTEdgeReleaseEnvironment(784, 23903, "Any name", VstsEnvironmentStatus.InProgress, new HashSet<IoTEdgeReleaseDeployment>())
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
                        ReleaseDefinitionId.E2ETest,
                        "Relase-2378",
                        "branch1",
                        VstsReleaseStatus.Active,
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
                ReleaseDefinitionId.E2ETest,
                "test-release",
                "branch1",
                VstsReleaseStatus.Active,
                new Uri("http://abc.com/test/uri"),
                new HashSet<IoTEdgeReleaseEnvironment>
                {
                    new IoTEdgeReleaseEnvironment(3423, 8790, "Any name", VstsEnvironmentStatus.NotStarted, new HashSet<IoTEdgeReleaseDeployment>()),
                    new IoTEdgeReleaseEnvironment(784, 23903, "Any name", VstsEnvironmentStatus.InProgress, new HashSet<IoTEdgeReleaseDeployment>())
                });

            Assert.AreEqual(123213, release.Id);
            Assert.AreEqual(ReleaseDefinitionId.E2ETest, release.DefinitionId);
            Assert.AreEqual("test-release", release.Name);
            Assert.AreEqual("http://abc.com/test/uri", release.WebUri.AbsoluteUri);
            Assert.AreEqual(2, release.NumberOfEnvironments);
            IoTEdgeReleaseEnvironment env1 = release.GetEnvironment(8790);
            Assert.AreEqual(3423, env1.Id);
            IoTEdgeReleaseEnvironment env2 = release.GetEnvironment(23903);
            Assert.AreEqual(784, env2.Id);
        }

        [Test]
        public void TestEquals()
        {
            DateTime deploymentStartTime = DateTime.UtcNow;
            var release1 = new IoTEdgeRelease(
                123213,
                ReleaseDefinitionId.E2ETest,
                "test-release",
                "branch1",
                VstsReleaseStatus.Active,
                new Uri("http://abc.com/test/uri"),
                new HashSet<IoTEdgeReleaseEnvironment>
                {
                    new IoTEdgeReleaseEnvironment(3423, 8790, "Any name", VstsEnvironmentStatus.NotStarted, new HashSet<IoTEdgeReleaseDeployment>()),
                    new IoTEdgeReleaseEnvironment(784, 23903, "Any name", VstsEnvironmentStatus.InProgress, new HashSet<IoTEdgeReleaseDeployment>())
                });

            var release2 = new IoTEdgeRelease(
                123213,
                ReleaseDefinitionId.E2ETest,
                "test-release",
                "branch1",
                VstsReleaseStatus.Active,
                new Uri("http://abc.com/test/uri"),
                new HashSet<IoTEdgeReleaseEnvironment>
                {
                    new IoTEdgeReleaseEnvironment(3423, 8790, "Any name", VstsEnvironmentStatus.NotStarted, new HashSet<IoTEdgeReleaseDeployment>()),
                    new IoTEdgeReleaseEnvironment(784, 23903, "Any name", VstsEnvironmentStatus.InProgress, new HashSet<IoTEdgeReleaseDeployment>())
                });

            var release3 = new IoTEdgeRelease(
                2343,
                ReleaseDefinitionId.E2ETest,
                "test-release",
                "branch1",
                VstsReleaseStatus.Active,
                new Uri("http://abc.com/test/uri"),
                new HashSet<IoTEdgeReleaseEnvironment>
                {
                    new IoTEdgeReleaseEnvironment(3423, 8790, "Any name", VstsEnvironmentStatus.NotStarted, new HashSet<IoTEdgeReleaseDeployment>()),
                    new IoTEdgeReleaseEnvironment(784, 23903, "Any name", VstsEnvironmentStatus.InProgress, new HashSet<IoTEdgeReleaseDeployment>())
                });

            var release4 = new IoTEdgeRelease(
                123213,
                ReleaseDefinitionId.E2ETest,
                "test-release",
                "branch1",
                VstsReleaseStatus.Active,
                new Uri("http://abc.com/test/uri"),
                new HashSet<IoTEdgeReleaseEnvironment>
                {
                    new IoTEdgeReleaseEnvironment(3423, 8790, "Any name", VstsEnvironmentStatus.NotStarted, new HashSet<IoTEdgeReleaseDeployment>()),
                    new IoTEdgeReleaseEnvironment(784, 23903, "Any name", VstsEnvironmentStatus.InProgress, new HashSet<IoTEdgeReleaseDeployment>())
                });

            var release5 = new IoTEdgeRelease(
                123213,
                ReleaseDefinitionId.E2ETest,
                "test-release342",
                "branch1",
                VstsReleaseStatus.Active,
                new Uri("http://abc.com/test/uri"),
                new HashSet<IoTEdgeReleaseEnvironment>
                {
                    new IoTEdgeReleaseEnvironment(3423, 8790, "Any name", VstsEnvironmentStatus.NotStarted, new HashSet<IoTEdgeReleaseDeployment>()),
                    new IoTEdgeReleaseEnvironment(784, 23903, "Any name", VstsEnvironmentStatus.InProgress, new HashSet<IoTEdgeReleaseDeployment>())
                });

            var release6 = new IoTEdgeRelease(
                123213,
                ReleaseDefinitionId.E2ETest,
                "test-release",
                "branch1",
                VstsReleaseStatus.Active,
                new Uri("http://abc.com/test/uri/123123"),
                new HashSet<IoTEdgeReleaseEnvironment>
                {
                    new IoTEdgeReleaseEnvironment(3423, 8790, "Any name", VstsEnvironmentStatus.NotStarted, new HashSet<IoTEdgeReleaseDeployment>()),
                    new IoTEdgeReleaseEnvironment(784, 23903, "Any name", VstsEnvironmentStatus.InProgress, new HashSet<IoTEdgeReleaseDeployment>())
                });

            var release7 = new IoTEdgeRelease(
                123213,
                ReleaseDefinitionId.E2ETest,
                "test-release",
                "branch1",
                VstsReleaseStatus.Active,
                new Uri("http://abc.com/test/uri/123123"),
                new HashSet<IoTEdgeReleaseEnvironment>
                {
                    new IoTEdgeReleaseEnvironment(3423, 8790, "Any name", VstsEnvironmentStatus.NotStarted, new HashSet<IoTEdgeReleaseDeployment>())
                });

            Assert.False(release1.Equals(null));
            Assert.True(release1.Equals(release1));
            Assert.True(release1.Equals(release2));

            Assert.False(release1.Equals((object) null));
            Assert.True(release1.Equals((object) release1));
            Assert.True(release1.Equals((object) release2));
            Assert.False(release1.Equals(new object()));

            Assert.False(release1.Equals(release3));
            Assert.True(release1.Equals(release4));
            Assert.False(release1.Equals(release5));
            Assert.False(release1.Equals(release6));
            Assert.False(release1.Equals(release7));
        }

        [Test]
        public void TestGetHashCode()
        {
            var release = new IoTEdgeRelease(
                123213,
                ReleaseDefinitionId.E2ETest,
                "test-release",
                "branch1",
                VstsReleaseStatus.Active,
                new Uri("http://abc.com/test/uri"),
                new HashSet<IoTEdgeReleaseEnvironment>
                {
                    new IoTEdgeReleaseEnvironment(3423, 8790, "Any name", VstsEnvironmentStatus.NotStarted, new HashSet<IoTEdgeReleaseDeployment>()),
                    new IoTEdgeReleaseEnvironment(784, 23903, "Any name", VstsEnvironmentStatus.InProgress, new HashSet<IoTEdgeReleaseDeployment>())
                });

            Assert.AreEqual(123213, release.GetHashCode());
        }

        [Test]
        public void TestGetEnvironment()
        {
            var release = new IoTEdgeRelease(
                123213,
                ReleaseDefinitionId.E2ETest,
                "test-release",
                "branch1",
                VstsReleaseStatus.Active,
                new Uri("http://abc.com/test/uri"),
                new HashSet<IoTEdgeReleaseEnvironment>
                {
                    new IoTEdgeReleaseEnvironment(3423, 8790, "Any name", VstsEnvironmentStatus.NotStarted, new HashSet<IoTEdgeReleaseDeployment>()),
                    new IoTEdgeReleaseEnvironment(784, 23903, "Any name", VstsEnvironmentStatus.InProgress, new HashSet<IoTEdgeReleaseDeployment>()),
                    new IoTEdgeReleaseEnvironment(38934, 23903, "Any name", VstsEnvironmentStatus.Succeeded, new HashSet<IoTEdgeReleaseDeployment>())
                });

            Assert.AreEqual(3423, release.GetEnvironment(8790).Id);
            Assert.AreEqual(784, release.GetEnvironment(23903).Id);
            Assert.AreEqual(IoTEdgeReleaseEnvironment.CreateEnvironmentWithNoResult(9999), release.GetEnvironment(9999));
        }
    }
}
