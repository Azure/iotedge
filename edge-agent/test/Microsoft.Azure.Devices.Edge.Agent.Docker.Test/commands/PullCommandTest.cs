// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Docker.Test.Commands
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Docker.Commands;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;
    using global::Docker.DotNet;
    using Moq;
    using global::Docker.DotNet.Models;

    [ExcludeFromCodeCoverage]
    [Collection("Docker")]
    public class PullCommandTest
    {

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

                    var config = new DockerConfig(Image, String.Empty);
                    var module = new DockerModule(Name, "1.0", ModuleStatus.Running, Core.RestartPolicy.OnUnhealthy, config, null);
                                        
                    ICommand pullCommand = new PullCommand(DockerHelper.Client, module, null);
                    
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
                //ImagesCreateParameters parameters, AuthConfig authConfig, IProgress<JSONMessage> progress, CancellationToken cancellationToken = default(CancellationToken)
                images.Setup(m => m.CreateImageAsync(It.IsAny<ImagesCreateParameters>(),
                                                     It.IsAny<AuthConfig>(),
                                                     It.IsAny<IProgress<JSONMessage>>(),
                                                     It.IsAny<CancellationToken>()))
                                                  .Throws(new DockerApiException(System.Net.HttpStatusCode.NotFound, "FakeResponseBody"));
                                
                var dockerClient = new Mock<IDockerClient>();
                dockerClient.SetupGet(c => c.Images).Returns(images.Object);

                var module = new DockerModule(Name, "1.0", ModuleStatus.Running, Core.RestartPolicy.OnUnhealthy, DockerConfig.Unknown, null);


                ICommand pullCommand = new PullCommand(dockerClient.Object, module, null);

                await Assert.ThrowsAsync<ImageNotFoundException>(() => pullCommand.ExecuteAsync(cts.Token));
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
                //ImagesCreateParameters parameters, AuthConfig authConfig, IProgress<JSONMessage> progress, CancellationToken cancellationToken = default(CancellationToken)
                images.Setup(m => m.CreateImageAsync(It.IsAny<ImagesCreateParameters>(),
                                                     It.IsAny<AuthConfig>(),
                                                     It.IsAny<IProgress<JSONMessage>>(),
                                                     It.IsAny<CancellationToken>()))
                                                  .Throws(new DockerApiException(System.Net.HttpStatusCode.Unauthorized, "FakeResponseBody"));

                var dockerClient = new Mock<IDockerClient>();
                dockerClient.SetupGet(c => c.Images).Returns(images.Object);

                var module = new DockerModule(Name, "1.0", ModuleStatus.Running, Core.RestartPolicy.OnUnhealthy, DockerConfig.Unknown, null);


                ICommand pullCommand = new PullCommand(dockerClient.Object, module, null);

                await Assert.ThrowsAsync<DockerApiException>(() => pullCommand.ExecuteAsync(cts.Token));
            }
        }
    }
}
