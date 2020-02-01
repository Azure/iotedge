// Copyright (c) Microsoft. All rights reserved.
namespace DevOpsLibTest
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Net.Http;
    using System.Threading.Tasks;
    using DevOpsLib;
    using DevOpsLib.VstsModels;
    using Flurl;
    using Flurl.Http.Testing;
    using NUnit.Framework;

    [TestFixture]
    public class BuildManagementTest
    {
        const string PersonalAccessToken = "pattoken123";

        BuildManagement buildManagement;
        HttpTest httpTest;

        [SetUp]
        public void Setup()
        {
            this.httpTest = new HttpTest();
            this.buildManagement = new BuildManagement(new DevOpsAccessSetting(PersonalAccessToken));
        }

        [TearDown]
        public void TearDown()
        {
            this.httpTest.Dispose();
        }

        [Test]
        public async Task TestGetLatestBuildsAsyncWithNoDefinitionId()
        {
            ArgumentException ex = Assert.ThrowsAsync<ArgumentException>(
                async () => { await this.buildManagement.GetLatestBuildsAsync(new HashSet<BuildDefinitionId>(), "anybranch").ConfigureAwait(false); });

            Assert.True(ex.Message.StartsWith("Cannot be null or empty collection."));
            Assert.That(ex.ParamName, Is.EqualTo("buildDefinitionIds"));
        }

        [Test]
        public async Task TestGetLatestBuildsAsyncWithEmptyBranchName()
        {
            ArgumentException ex = Assert.ThrowsAsync<ArgumentException>(
                async () => { await this.buildManagement.GetLatestBuildsAsync(BuildExtension.MasterBranchReporting, " ").ConfigureAwait(false); });

            Assert.True(ex.Message.StartsWith("Cannot be null or white space."));
            Assert.That(ex.ParamName, Is.EqualTo("branchName"));
        }

        [Test]
        public async Task TestGetLatestBuildsAsyncWithBuildResults()
        {
            this.httpTest.RespondWithJson(
                new
                {
                    count = 2,
                    value = new object[]
                    {
                        new
                        {
                            definition = new
                            {
                                id = BuildDefinitionId.BuildImages
                            },
                            buildNumber = "20191031.6",
                            _links = new
                            {
                                sourceVersionDisplayUri = new
                                {
                                    href = "https://dev.azure.com/msazure/b3jdur1e-8e232-41b2-9d23-5bc261fd2ou4/_apis/build/builds/26326316/sources"
                                },
                                web = new
                                {
                                    href = "https://dev.azure.com/msazure/b3jdur1e-8e232-41b2-9d23-5bc261fd2ou4/_build/results?buildId=26326316"
                                }
                            },
                            sourceBranch = "refs/heads/master",
                            status = "completed",
                            result = "failed",
                            queueTime = "2019-10-31T23:24:00.6625531Z",
                            startTime = "2019-10-31T23:40:05.7369767Z",
                            finishTime = "2019-11-01T00:09:00.3460115Z"
                        },
                        new
                        {
                            definition = new
                            {
                                id = BuildDefinitionId.CI
                            },
                            buildNumber = "20191030.2",
                            _links = new
                            {
                                sourceVersionDisplayUri = new
                                {
                                    href = "https://dev.azure.com/msazure/b3jdur1e-8e232-41b2-9d23-5bc261fd2ou4/_apis/build/builds/25980152/sources"
                                },
                                web = new
                                {
                                    href = "https://dev.azure.com/msazure/b3jdur1e-8e232-41b2-9d23-5bc261fd2ou4/_build/results?buildId=25980152"
                                }
                            },
                            sourceBranch = "refs/heads/master",
                            status = "inProgress",
                            queueTime = "2019-10-31T23:24:04.4525827Z",
                            startTime = "2019-10-31T23:24:20.6743184Z"
                        }
                    }
                });

            string branch = "refs/heads/master";
            IList<VstsBuild> latestBuilds = await this.buildManagement.GetLatestBuildsAsync(
                new HashSet<BuildDefinitionId> { BuildDefinitionId.BuildImages, BuildDefinitionId.CI, BuildDefinitionId.EdgeletCI },
                branch).ConfigureAwait(false);

            string definitionIdsDelimitedValues = Url.Encode(string.Join(",", new[] { BuildDefinitionId.BuildImages.IdString(), BuildDefinitionId.CI.IdString(), BuildDefinitionId.EdgeletCI.IdString() }));
            string requestUri = $"https://dev.azure.com/{DevOpsAccessSetting.AzureOrganization}/{DevOpsAccessSetting.AzureProject}/_apis/build/builds?definitions={definitionIdsDelimitedValues}&queryOrder=finishTimeDescending&maxBuildsPerDefinition=1&api-version=5.1&branchName={Url.Encode(branch)}";
            this.httpTest.ShouldHaveCalled(requestUri)
                .WithVerb(HttpMethod.Get)
                .WithBasicAuth(string.Empty, PersonalAccessToken)
                .Times(1);

            Assert.AreEqual(latestBuilds.Count, 3);

            var ciBuild = latestBuilds.First(b => b.DefinitionId == BuildDefinitionId.CI);
            Assert.AreEqual("20191030.2", ciBuild.BuildNumber);
            Assert.AreEqual("https://dev.azure.com/msazure/b3jdur1e-8e232-41b2-9d23-5bc261fd2ou4/_apis/build/builds/25980152/sources", ciBuild.SourceVersionDisplayUri.AbsoluteUri);
            Assert.AreEqual("https://dev.azure.com/msazure/b3jdur1e-8e232-41b2-9d23-5bc261fd2ou4/_build/results?buildId=25980152", ciBuild.WebUri.AbsoluteUri);
            Assert.AreEqual(VstsBuildStatus.InProgress, ciBuild.Status);
            Assert.AreEqual(VstsBuildResult.None, ciBuild.Result);
            Assert.AreEqual("10/31/2019 23:24:04", ciBuild.QueueTime.ToString(CultureInfo.InvariantCulture));
            Assert.AreEqual("10/31/2019 23:24:20", ciBuild.StartTime.ToString(CultureInfo.InvariantCulture));
            Assert.AreEqual(DateTime.MinValue, ciBuild.FinishTime);

            var imagesBuild = latestBuilds.First(b => b.DefinitionId == BuildDefinitionId.BuildImages);
            Assert.AreEqual("20191031.6", imagesBuild.BuildNumber);
            Assert.AreEqual("https://dev.azure.com/msazure/b3jdur1e-8e232-41b2-9d23-5bc261fd2ou4/_apis/build/builds/26326316/sources", imagesBuild.SourceVersionDisplayUri.AbsoluteUri);
            Assert.AreEqual("https://dev.azure.com/msazure/b3jdur1e-8e232-41b2-9d23-5bc261fd2ou4/_build/results?buildId=26326316", imagesBuild.WebUri.AbsoluteUri);
            Assert.AreEqual(VstsBuildStatus.Completed, imagesBuild.Status);
            Assert.AreEqual(VstsBuildResult.Failed, imagesBuild.Result);
            Assert.AreEqual("10/31/2019 23:24:00", imagesBuild.QueueTime.ToString(CultureInfo.InvariantCulture));
            Assert.AreEqual("10/31/2019 23:40:05", imagesBuild.StartTime.ToString(CultureInfo.InvariantCulture));
            Assert.AreEqual("11/01/2019 00:09:00", imagesBuild.FinishTime.ToString(CultureInfo.InvariantCulture));

            VerifyEmptyBuildResult(latestBuilds.First(b => b.DefinitionId == BuildDefinitionId.EdgeletCI));
        }

        [Test]
        public async Task TestGetLatestBuildsAsyncWithNoBuild()
        {
            this.httpTest.RespondWithJson(
                new
                {
                    count = 0,
                    value = new object[] { }
                });

            string branch = "refs/heads/master";
            IList<VstsBuild> latestBuilds = await this.buildManagement.GetLatestBuildsAsync(
                new HashSet<BuildDefinitionId> { BuildDefinitionId.BuildImages, BuildDefinitionId.CI },
                branch).ConfigureAwait(false);

            string definitionIdsDelimitedValues = Url.Encode(string.Join(",", new[] { BuildDefinitionId.BuildImages.IdString(), BuildDefinitionId.CI.IdString() }));
            string requestUri = $"https://dev.azure.com/{DevOpsAccessSetting.AzureOrganization}/{DevOpsAccessSetting.AzureProject}/_apis/build/builds?definitions={definitionIdsDelimitedValues}&queryOrder=finishTimeDescending&maxBuildsPerDefinition=1&api-version=5.1&branchName={Url.Encode(branch)}";
            this.httpTest.ShouldHaveCalled(requestUri)
                .WithVerb(HttpMethod.Get)
                .WithBasicAuth(string.Empty, PersonalAccessToken)
                .Times(1);

            Assert.AreEqual( 2, latestBuilds.Count);
            VerifyEmptyBuildResult(latestBuilds.First(b => b.DefinitionId == BuildDefinitionId.BuildImages));
            VerifyEmptyBuildResult(latestBuilds.First(b => b.DefinitionId == BuildDefinitionId.CI));
        }

        public async Task TestGetLatestBuildsAsyncWithUnexpectedResult()
        {
            this.httpTest.RespondWithJson(
                new
                {
                    unexpected = new {}
                });

            string branch = "refs/heads/master";
            IList<VstsBuild> latestBuilds = await this.buildManagement.GetLatestBuildsAsync(
                new HashSet<BuildDefinitionId> { BuildDefinitionId.BuildImages, BuildDefinitionId.CI },
                branch).ConfigureAwait(false);

            string definitionIdsDelimitedValues = Url.Encode(string.Join(",", new[] { BuildDefinitionId.BuildImages.IdString(), BuildDefinitionId.CI.IdString() }));
            string requestUri = $"https://dev.azure.com/{DevOpsAccessSetting.AzureOrganization}/{DevOpsAccessSetting.AzureProject}/_apis/build/builds?definitions={definitionIdsDelimitedValues}&queryOrder=finishTimeDescending&maxBuildsPerDefinition=1&api-version=5.1&branchName={Url.Encode(branch)}";
            this.httpTest.ShouldHaveCalled(requestUri)
                .WithVerb(HttpMethod.Get)
                .WithBasicAuth(string.Empty, PersonalAccessToken)
                .Times(1);

            Assert.AreEqual(2, latestBuilds.Count);
            VerifyEmptyBuildResult(latestBuilds.First(b => b.DefinitionId == BuildDefinitionId.BuildImages));
            VerifyEmptyBuildResult(latestBuilds.First(b => b.DefinitionId == BuildDefinitionId.CI));
        }

        static void VerifyEmptyBuildResult(VstsBuild build)
        {
            Assert.AreEqual(string.Empty, build.BuildNumber);
            Assert.AreEqual("https://dev.azure.com/msazure/One/_build", build.WebUri.AbsoluteUri);
            Assert.AreEqual("https://dev.azure.com/msazure/One/_build", build.WebUri.AbsoluteUri);
            Assert.AreEqual(VstsBuildStatus.None, build.Status);
            Assert.AreEqual(VstsBuildResult.None, build.Result);
            Assert.AreEqual(DateTime.MinValue, build.QueueTime);
            Assert.AreEqual(DateTime.MinValue, build.StartTime);
            Assert.AreEqual(DateTime.MinValue, build.FinishTime);
        }
    }
}
