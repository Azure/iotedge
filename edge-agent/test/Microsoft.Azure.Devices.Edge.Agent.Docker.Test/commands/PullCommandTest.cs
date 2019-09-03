// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Docker.Test.Commands
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using global::Docker.DotNet;
    using global::Docker.DotNet.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Docker.Commands;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;
    using CreateContainerParameters = Microsoft.Azure.Devices.Edge.Agent.Docker.Models.CreateContainerParameters;

    [ExcludeFromCodeCoverage]
    [Collection("Docker")]
    public class PullCommandTest
    {
        static readonly Option<AuthConfig> NoAuth = Option.None<AuthConfig>();

        public static IEnumerable<object[]> CreateTestData()
        {
            (string testFullImage, string image, string tag)[] testInputRecords =
            {
                ("localhost:5000/edge-hub:latest", "localhost:5000/edge-hub", "latest"),
                ("edgebuilds.azurecr.io/azedge-edge-agent-x64:latest", "edgebuilds.azurecr.io/azedge-edge-agent-x64", "latest"),
                ("mongo:3.4.4", "mongo", "3.4.4"),
                ("edgebuilds.azurecr.io/azedge-simulated-temperature-sensor-x64", "edgebuilds.azurecr.io/azedge-simulated-temperature-sensor-x64", string.Empty)
            };
            return testInputRecords.Select(r => new object[] { r.testFullImage, r.image, r.tag }).AsEnumerable();
        }

        [Theory]
        [Unit]
        [MemberData(nameof(CreateTestData))]
        public async Task PullValidImages(string testFullImage, string image, string tag)
        {
            // Arrange
            string testImage = string.Empty;
            string testTag = string.Empty;
            var auth = new AuthConfig();
            var client = new Mock<IDockerClient>();
            var images = new Mock<IImageOperations>();
            images.Setup(
                    i => i.CreateImageAsync(
                        It.IsAny<ImagesCreateParameters>(),
                        It.IsAny<AuthConfig>(),
                        It.IsAny<IProgress<JSONMessage>>(),
                        It.IsAny<CancellationToken>()))
                .Callback<ImagesCreateParameters, AuthConfig, IProgress<JSONMessage>, CancellationToken>(
                    (icp, a, p, t) =>
                    {
                        testImage = icp.FromImage;
                        testTag = icp.Tag;
                    })
                .Returns(TaskEx.Done);
            client.SetupGet(c => c.Images).Returns(images.Object);

            var config = new CombinedDockerConfig(testFullImage, new CreateContainerParameters(), Option.Some(auth));

            // Act
            var command = new PullCommand(client.Object, config);

            await command.ExecuteAsync(CancellationToken.None);

            // Assert
            client.VerifyAll();
            images.VerifyAll();

            Assert.Equal(image, testImage);
            Assert.Equal(tag, testTag);
        }

        [Fact]
        [Integration]
        public async Task ImageNotFoundTest()
        {
            const string Image = "non-existing-image:latest";
            const string Name = "non-existing-image-name";
            try
            {
                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15)))
                {
                    await DockerHelper.Client.CleanupContainerAsync(Name, Image);

                    var config = new CombinedDockerConfig(Image, new CreateContainerParameters(), NoAuth);

                    ICommand pullCommand = new PullCommand(DockerHelper.Client, config);
                    await Assert.ThrowsAsync<ImageNotFoundException>(() => pullCommand.ExecuteAsync(cts.Token));
                }
            }
            finally
            {
                await DockerHelper.Client.CleanupContainerAsync(Name, Image);
            }
        }

        [Fact]
        [Unit]
        public async Task ImageNotFoundUnitTest()
        {
            const string Name = "non-existing-image-name";
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15)))
            {
                var images = new Mock<IImageOperations>();
                // ImagesCreateParameters parameters, AuthConfig authConfig, IProgress<JSONMessage> progress, CancellationToken cancellationToken = default(CancellationToken)
                images.Setup(
                        m => m.CreateImageAsync(
                            It.IsAny<ImagesCreateParameters>(),
                            It.IsAny<AuthConfig>(),
                            It.IsAny<IProgress<JSONMessage>>(),
                            It.IsAny<CancellationToken>()))
                    .Throws(new DockerApiException(HttpStatusCode.NotFound, "FakeResponseBody"));

                var dockerClient = new Mock<IDockerClient>();
                dockerClient.SetupGet(c => c.Images).Returns(images.Object);

                var config = new CombinedDockerConfig(Name, new CreateContainerParameters(), NoAuth);
                ICommand pullCommand = new PullCommand(dockerClient.Object, config);

                await Assert.ThrowsAsync<ImageNotFoundException>(() => pullCommand.ExecuteAsync(cts.Token));
            }
        }

        [Fact]
        [Unit]
        public async Task InternalServerErrorUnitTest()
        {
            const string Name = "non-existing-image-name";
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15)))
            {
                var images = new Mock<IImageOperations>();
                // ImagesCreateParameters parameters, AuthConfig authConfig, IProgress<JSONMessage> progress, CancellationToken cancellationToken = default(CancellationToken)
                images.Setup(
                        m => m.CreateImageAsync(
                            It.IsAny<ImagesCreateParameters>(),
                            It.IsAny<AuthConfig>(),
                            It.IsAny<IProgress<JSONMessage>>(),
                            It.IsAny<CancellationToken>()))
                    .Throws(new DockerApiException(HttpStatusCode.InternalServerError, "FakeResponseBody"));

                var dockerClient = new Mock<IDockerClient>();
                dockerClient.SetupGet(c => c.Images).Returns(images.Object);

                var config = new CombinedDockerConfig(Name, new CreateContainerParameters(), NoAuth);
                ICommand pullCommand = new PullCommand(dockerClient.Object, config);

                await Assert.ThrowsAsync<InternalServerErrorException>(() => pullCommand.ExecuteAsync(cts.Token));
            }
        }

        [Fact]
        [Unit]
        public async Task DockerApiExceptionDifferentFromNotFoundUnitTest()
        {
            const string Name = "non-existing-image-name";
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15)))
            {
                var images = new Mock<IImageOperations>();
                // ImagesCreateParameters parameters, AuthConfig authConfig, IProgress<JSONMessage> progress, CancellationToken cancellationToken = default(CancellationToken)
                images.Setup(
                        m => m.CreateImageAsync(
                            It.IsAny<ImagesCreateParameters>(),
                            It.IsAny<AuthConfig>(),
                            It.IsAny<IProgress<JSONMessage>>(),
                            It.IsAny<CancellationToken>()))
                    .Throws(new DockerApiException(HttpStatusCode.Unauthorized, "FakeResponseBody"));

                var dockerClient = new Mock<IDockerClient>();
                dockerClient.SetupGet(c => c.Images).Returns(images.Object);

                var config = new CombinedDockerConfig(Name, new CreateContainerParameters(), NoAuth);
                ICommand pullCommand = new PullCommand(dockerClient.Object, config);

                await Assert.ThrowsAsync<DockerApiException>(() => pullCommand.ExecuteAsync(cts.Token));
            }
        }
    }
}
