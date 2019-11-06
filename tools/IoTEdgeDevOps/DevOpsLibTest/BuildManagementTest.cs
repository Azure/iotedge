// Copyright (c) Microsoft. All rights reserved.
namespace DevOpsLibTest
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
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
                async () => { await this.buildManagement.GetLatestBuildsAsync(new int[] { }, "anybranch").ConfigureAwait(false); });

            Assert.True(ex.Message.StartsWith("Cannot be null or empty collection."));
            Assert.That(ex.ParamName, Is.EqualTo("buildDefinitionIds"));
        }

        [Test]
        public async Task TestGetLatestBuildsAsyncWithEmptyBranchName()
        {
            ArgumentException ex = Assert.ThrowsAsync<ArgumentException>(
                async () => { await this.buildManagement.GetLatestBuildsAsync(BuildDefinitionIds.MasterBranchReporting, " ").ConfigureAwait(false); });

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
                                id = BuildDefinitionIds.BuildImages
                            },
                            buildNumber = "20191031.6",
                            _links = new
                            {
                                sourceVersionDisplayUri = new
                                {
                                    href = "https://dev.azure.com/msazure/b32aa71e-8ed2-41b2-9d77-5bc261222004/_apis/build/builds/26326316/sources"
                                },
                                web = new
                                {
                                    href = "https://dev.azure.com/msazure/b32aa71e-8ed2-41b2-9d77-5bc261222004/_build/results?buildId=26326316"
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
                                id = BuildDefinitionIds.CI
                            },
                            buildNumber = "20191030.2",
                            _links = new
                            {
                                sourceVersionDisplayUri = new
                                {
                                    href = "https://dev.azure.com/msazure/b32aa71e-8ed2-41b2-9d77-5bc261222004/_apis/build/builds/25980152/sources"
                                },
                                web = new
                                {
                                    href = "https://dev.azure.com/msazure/b32aa71e-8ed2-41b2-9d77-5bc261222004/_build/results?buildId=25980152"
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
            IDictionary<int, VstsBuild> latestBuilds = await this.buildManagement.GetLatestBuildsAsync(
                new[] { BuildDefinitionIds.BuildImages, BuildDefinitionIds.CI, BuildDefinitionIds.EdgeletCI },
                branch).ConfigureAwait(false);

            string definitionIdsDelimitedValues = Url.Encode(string.Join(",", new[] { BuildDefinitionIds.BuildImages, BuildDefinitionIds.CI, BuildDefinitionIds.EdgeletCI }));
            string requestUri = $"https://dev.azure.com/{DevOpsAccessSetting.AzureOrganization}/{DevOpsAccessSetting.AzureProject}/_apis/build/builds?definitions={definitionIdsDelimitedValues}&queryOrder=finishTimeDescending&maxBuildsPerDefinition=1&api-version=5.1&branchName={Url.Encode(branch)}";
            this.httpTest.ShouldHaveCalled(requestUri)
                .WithVerb(HttpMethod.Get)
                .WithBasicAuth(string.Empty, PersonalAccessToken)
                .Times(1);

            Assert.AreEqual(latestBuilds.Count, 3);

            var ciBuild = latestBuilds[BuildDefinitionIds.CI];
            Assert.AreEqual("20191030.2", ciBuild.BuildNumber);
            Assert.AreEqual("https://dev.azure.com/msazure/b32aa71e-8ed2-41b2-9d77-5bc261222004/_apis/build/builds/25980152/sources", ciBuild.SourceVersionDisplayUri.AbsoluteUri);
            Assert.AreEqual("https://dev.azure.com/msazure/b32aa71e-8ed2-41b2-9d77-5bc261222004/_build/results?buildId=25980152", ciBuild.WebUri.AbsoluteUri);
            Assert.AreEqual(VstsBuildStatus.InProgress, ciBuild.Status);
            Assert.AreEqual(VstsBuildResult.None, ciBuild.Result);
            Assert.AreEqual("10/31/2019 23:24:04", ciBuild.QueueTime.ToString(CultureInfo.InvariantCulture));
            Assert.AreEqual("10/31/2019 23:24:20", ciBuild.StartTime.ToString(CultureInfo.InvariantCulture));
            Assert.AreEqual(DateTime.MinValue, ciBuild.FinishTime);

            var imagesBuild = latestBuilds[BuildDefinitionIds.BuildImages];
            Assert.AreEqual("20191031.6", imagesBuild.BuildNumber);
            Assert.AreEqual("https://dev.azure.com/msazure/b32aa71e-8ed2-41b2-9d77-5bc261222004/_apis/build/builds/26326316/sources", imagesBuild.SourceVersionDisplayUri.AbsoluteUri);
            Assert.AreEqual("https://dev.azure.com/msazure/b32aa71e-8ed2-41b2-9d77-5bc261222004/_build/results?buildId=26326316", imagesBuild.WebUri.AbsoluteUri);
            Assert.AreEqual(VstsBuildStatus.Completed, imagesBuild.Status);
            Assert.AreEqual(VstsBuildResult.Failed, imagesBuild.Result);
            Assert.AreEqual("10/31/2019 23:24:00", imagesBuild.QueueTime.ToString(CultureInfo.InvariantCulture));
            Assert.AreEqual("10/31/2019 23:40:05", imagesBuild.StartTime.ToString(CultureInfo.InvariantCulture));
            Assert.AreEqual("11/01/2019 00:09:00", imagesBuild.FinishTime.ToString(CultureInfo.InvariantCulture));

            VerifyEmptyBuildResult(latestBuilds[BuildDefinitionIds.EdgeletCI]);
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
            IDictionary<int, VstsBuild> latestBuilds = await this.buildManagement.GetLatestBuildsAsync(
                new[] { BuildDefinitionIds.BuildImages, BuildDefinitionIds.CI },
                branch).ConfigureAwait(false);

            string definitionIdsDelimitedValues = Url.Encode(string.Join(",", new[] { BuildDefinitionIds.BuildImages, BuildDefinitionIds.CI }));
            string requestUri = $"https://dev.azure.com/{DevOpsAccessSetting.AzureOrganization}/{DevOpsAccessSetting.AzureProject}/_apis/build/builds?definitions={definitionIdsDelimitedValues}&queryOrder=finishTimeDescending&maxBuildsPerDefinition=1&api-version=5.1&branchName={Url.Encode(branch)}";
            this.httpTest.ShouldHaveCalled(requestUri)
                .WithVerb(HttpMethod.Get)
                .WithBasicAuth(string.Empty, PersonalAccessToken)
                .Times(1);

            Assert.AreEqual(latestBuilds.Count, 2);
            VerifyEmptyBuildResult(latestBuilds[BuildDefinitionIds.BuildImages]);
            VerifyEmptyBuildResult(latestBuilds[BuildDefinitionIds.CI]);
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
