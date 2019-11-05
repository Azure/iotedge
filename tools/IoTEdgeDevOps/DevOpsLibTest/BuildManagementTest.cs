// Copyright (c) Microsoft. All rights reserved.
namespace DevOpsLibTest
{
    using System;
    using System.Globalization;
    using System.Net.Http;
    using System.Threading.Tasks;
    using DevOpsLib;
    using DevOpsLib.VstsModels;
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
        public async Task TestGetLatestBuildAsync()
        {
            this.httpTest.RespondWithJson(
                new
                {
                    buildNumber = "20191019.2",
                    _links = new
                    {
                        web = new {
                            href = "https://dev.azure.com/msazure/b32aa71e-8ed2-41b2-9d77-5bc261222004/_build/results?buildId=25980152"
                        }
                    },
                    status = "completed",
                    result = "succeeded",
                    queueTime = "2019-10-19T16:57:09.3224324Z",
                    startTime = "2019-10-19T16:58:37.7494889Z",
                    finishTime = "2019-10-19T17:42:44.0001429Z"
                });

            VstsBuild latestBuild = await this.buildManagement.GetLatestBuildAsync(BuildDefinitionIds.CI, "master").ConfigureAwait(false);

            this.httpTest.ShouldHaveCalled($"https://dev.azure.com/{DevOpsAccessSetting.AzureOrganization}/{DevOpsAccessSetting.AzureProject}/_apis/build/latest/{BuildDefinitionIds.CI}?api-version=5.1-preview.1&branchName=master")
                .WithVerb(HttpMethod.Get)
                .WithBasicAuth(string.Empty, PersonalAccessToken)
                .Times(1);

            Assert.NotNull(latestBuild);
            Assert.AreEqual("20191019.2", latestBuild.BuildNumber);
            Assert.AreEqual("https://dev.azure.com/msazure/b32aa71e-8ed2-41b2-9d77-5bc261222004/_build/results?buildId=25980152", latestBuild.WebUri.AbsoluteUri);
            Assert.AreEqual(VstsBuildStatus.Completed, latestBuild.Status);
            Assert.AreEqual(VstsBuildResult.Succeeded, latestBuild.Result);
            Assert.AreEqual("10/19/2019 16:57:09", latestBuild.QueueTime.ToString(CultureInfo.InvariantCulture));
            Assert.AreEqual("10/19/2019 16:58:37", latestBuild.StartTime.ToString(CultureInfo.InvariantCulture));
            Assert.AreEqual("10/19/2019 17:42:44", latestBuild.FinishTime.ToString(CultureInfo.InvariantCulture));
        }

        [Test]
        public async Task TestGetLatestBuildAsyncWithNoBuild()
        {
            this.httpTest.RespondWithJson(
                new
                {
                    message = "No completed builds were found for pipeline 31845.",
                    typeName = "Microsoft.TeamFoundation.Build.WebApi.BuildNotFoundException, Microsoft.TeamFoundation.Build2.WebApi",
                    typeKey = "BuildNotFoundException",
                    errorCode = 0,
                    eventId = 3000
                });

            VstsBuild latestBuild = await this.buildManagement.GetLatestBuildAsync(BuildDefinitionIds.CI, "master").ConfigureAwait(false);

            this.httpTest.ShouldHaveCalled($"https://dev.azure.com/{DevOpsAccessSetting.AzureOrganization}/{DevOpsAccessSetting.AzureProject}/_apis/build/latest/{BuildDefinitionIds.CI}?api-version=5.1-preview.1&branchName=master")
                .WithVerb(HttpMethod.Get)
                .WithBasicAuth(string.Empty, PersonalAccessToken)
                .Times(1);

            Assert.NotNull(latestBuild);
            Assert.IsNull(latestBuild.BuildNumber);
            Assert.AreEqual(VstsBuildStatus.None, latestBuild.Status);
            Assert.AreEqual(VstsBuildResult.None, latestBuild.Result);
            Assert.AreEqual("01/01/0001 00:00:00", latestBuild.QueueTime.ToString(CultureInfo.InvariantCulture));
            Assert.AreEqual("01/01/0001 00:00:00", latestBuild.StartTime.ToString(CultureInfo.InvariantCulture));
            Assert.AreEqual("01/01/0001 00:00:00", latestBuild.FinishTime.ToString(CultureInfo.InvariantCulture));
        }
    }
}
