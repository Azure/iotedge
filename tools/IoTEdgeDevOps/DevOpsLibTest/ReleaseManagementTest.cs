// Copyright (c) Microsoft. All rights reserved.
namespace DevOpsLibTest
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading.Tasks;
    using DevOpsLib;
    using DevOpsLib.VstsModels;
    using Flurl;
    using Flurl.Http.Testing;
    using NUnit.Framework;

    public class ReleaseManagementTest
    {
        const string PersonalAccessToken = "pattoken999";
        ReleaseManagement releaseManagement;
        HttpTest httpTest;

        [SetUp]
        public void Setup()
        {
            this.httpTest = new HttpTest();
            this.releaseManagement = new ReleaseManagement(new DevOpsAccessSetting(PersonalAccessToken));
        }

        [TearDown]
        public void TearDown()
        {
            this.httpTest.Dispose();
        }

        [Test]
        public async Task TestGetReleasesAsyncWithEmptyBranchNameAsync()
        {
            ArgumentException ex = Assert.ThrowsAsync<ArgumentException>(
                async () => { await this.releaseManagement.GetReleasesAsync(ReleaseDefinitionId.E2ETest, "").ConfigureAwait(false); });

            Assert.True(ex.Message.StartsWith("Cannot be null or white space."));
            Assert.That(ex.ParamName, Is.EqualTo("branchName"));
        }

        [Test]
        public async Task TestGetReleasesAsyncWithBuildResults()
        {
            this.httpTest.RespondWithJson(
                new
                {
                    count = 2,
                    value = new object[]
                    {
                        new
                        {
                            id = 1429321,
                            releaseDefinition = new
                            {
                                id = 2189
                            },
                            name = "Release-1766",
                            _links = new
                            {
                                web = new
                                {
                                    href = "https://dev.azure.com/msazure/b32aa71e-8ed2-41b2-9d77-5bc261222004/_release?releaseId=1429321&_a=release-summary"
                                }
                            },
                            environments = new object[]
                            {
                                new
                                {
                                    id = 3660597,
                                    definitionEnvironmentId = 10073,
                                    status = "succeeded"
                                },
                                new
                                {
                                    id = 3665008,
                                    definitionEnvironmentId = 10538,
                                    status = "InProgress"
                                }
                            }
                        },
                        new
                        {
                            id = 1429401,
                            releaseDefinition = new
                            {
                                id = 2189
                            },
                            name = "Release-1765",
                            _links = new
                            {
                                web = new
                                {
                                    href = "https://dev.azure.com/msazure/b32aa71e-8ed2-41b2-9d77-5bc261222004/_release?releaseId=1429401&_a=release-summary"
                                }
                            },
                            environments = new object[]
                            {
                                new
                                {
                                    id = 3653535,
                                    definitionEnvironmentId = 10073,
                                    status = "partiallySucceeded"
                                },
                                new
                                {
                                    id = 3653536,
                                    definitionEnvironmentId = 10538,
                                    status = "Queued"
                                }
                            }
                        }
                    }
                });

            string branch = "refs/heads/master";
            List<IoTEdgeRelease> releases = await this.releaseManagement.GetReleasesAsync(ReleaseDefinitionId.E2ETest, branch).ConfigureAwait(false);

            string requestUri = $"https://vsrm.dev.azure.com/{DevOpsAccessSetting.AzureOrganization}/{DevOpsAccessSetting.AzureProject}/_apis/release/releases?definitionId={ReleaseDefinitionId.E2ETest.IdString()}&queryOrder=descending&$expand=environments&statusFilter=active&$top=5&api-version=5.1&sourceBranchFilter={Url.Encode(branch)}";
            this.httpTest.ShouldHaveCalled(requestUri)
                .WithVerb(HttpMethod.Get)
                .WithBasicAuth(string.Empty, PersonalAccessToken)
                .Times(1);

            Assert.AreEqual(2, releases.Count);

            Assert.AreEqual(1429321, releases[0].Id);
            Assert.AreEqual((int)ReleaseDefinitionId.E2ETest, releases[0].DefinitionId);
            Assert.AreEqual("Release-1766", releases[0].Name);
            Assert.AreEqual("https://dev.azure.com/msazure/b32aa71e-8ed2-41b2-9d77-5bc261222004/_release?releaseId=1429321&_a=release-summary", releases[0].WebUri.AbsoluteUri);
            Assert.AreEqual(2, releases[0].NumberOfEnvironments);
            var release1Env1 = releases[0].GetEnvironment(10073);
            Assert.AreEqual(3660597, release1Env1.Id);
            Assert.AreEqual(VstsEnvironmentStatus.Succeeded, release1Env1.Status);
            var release1Env2 = releases[0].GetEnvironment(10538);
            Assert.AreEqual(3665008, release1Env2.Id);
            Assert.AreEqual(VstsEnvironmentStatus.InProgress, release1Env2.Status);

            Assert.AreEqual(1429401, releases[1].Id);
            Assert.AreEqual((int)ReleaseDefinitionId.E2ETest, releases[1].DefinitionId);
            Assert.AreEqual("Release-1765", releases[1].Name);
            Assert.AreEqual("https://dev.azure.com/msazure/b32aa71e-8ed2-41b2-9d77-5bc261222004/_release?releaseId=1429401&_a=release-summary", releases[1].WebUri.AbsoluteUri);
            Assert.AreEqual(2, releases[1].NumberOfEnvironments);
            var release2Env1 = releases[1].GetEnvironment(10073);
            Assert.AreEqual(3653535, release2Env1.Id);
            Assert.AreEqual(VstsEnvironmentStatus.PartiallySucceeded, release2Env1.Status);
            var release2Env2 = releases[1].GetEnvironment(10538);
            Assert.AreEqual(3653536, release2Env2.Id);
            Assert.AreEqual(VstsEnvironmentStatus.Queued, release2Env2.Status);
        }

        [Test]
        public async Task TestGetReleasesAsyncWithNoBuild()
        {
            this.httpTest.RespondWithJson(
                new
                {
                    count = 0,
                    value = new object[] { }
                });

            string branch = "refs/heads/master";
            List<IoTEdgeRelease> releases = await this.releaseManagement.GetReleasesAsync(ReleaseDefinitionId.E2ETest, branch).ConfigureAwait(false);

            string requestUri = $"https://vsrm.dev.azure.com/{DevOpsAccessSetting.AzureOrganization}/{DevOpsAccessSetting.AzureProject}/_apis/release/releases?definitionId={ReleaseDefinitionId.E2ETest.IdString()}&queryOrder=descending&$expand=environments&statusFilter=active&$top=5&api-version=5.1&sourceBranchFilter={Url.Encode(branch)}";
            this.httpTest.ShouldHaveCalled(requestUri)
                .WithVerb(HttpMethod.Get)
                .WithBasicAuth(string.Empty, PersonalAccessToken)
                .Times(1);

            Assert.AreEqual(0, releases.Count);
        }

        [Test]
        public async Task TestGetLatestBuildsAsyncWithUnexpectedResult()
        {
            this.httpTest.RespondWithJson(
                new
                {
                    unexpected = new { }
                });

            string branch = "refs/heads/master";
            List<IoTEdgeRelease> releases = await this.releaseManagement.GetReleasesAsync(ReleaseDefinitionId.E2ETest, branch).ConfigureAwait(false);

            string requestUri = $"https://vsrm.dev.azure.com/{DevOpsAccessSetting.AzureOrganization}/{DevOpsAccessSetting.AzureProject}/_apis/release/releases?definitionId={ReleaseDefinitionId.E2ETest.IdString()}&queryOrder=descending&$expand=environments&statusFilter=active&$top=5&api-version=5.1&sourceBranchFilter={Url.Encode(branch)}";
            this.httpTest.ShouldHaveCalled(requestUri)
                .WithVerb(HttpMethod.Get)
                .WithBasicAuth(string.Empty, PersonalAccessToken)
                .Times(1);

            Assert.AreEqual(0, releases.Count);
        }
    }
}
