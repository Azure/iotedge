// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub.Test
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Common.Exceptions;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;

    [Unit]
    public class RetryingServiceClientTest
    {
        [Fact]
        public async Task GetModulesRetryTest()
        {
            // Arrange
            var underlying = new Mock<IServiceClient>();
            underlying.SetupSequence(c => c.GetModules())
                .Throws(new InvalidOperationException())
                .Returns(Task.FromResult(Enumerable.Empty<Module>()));
            var serviceClient = new RetryingServiceClient(underlying.Object);

            // Act
            IEnumerable<Module> modules = await serviceClient.GetModules();

            // Assert
            Assert.NotNull(modules);
        }

        [Fact]
        public async Task GetModuleRetryTest()
        {
            // Arrange
            var underlying = new Mock<IServiceClient>();
            underlying.SetupSequence(c => c.GetModule(It.IsAny<string>()))
                .Throws(new InvalidOperationException())
                .Returns(Task.FromResult(new Module("d1", "m1")));
            var serviceClient = new RetryingServiceClient(underlying.Object);

            // Act
            Module module = await serviceClient.GetModule("m1");

            // Assert
            Assert.NotNull(module);
            Assert.Equal("d1", module.DeviceId);
            Assert.Equal("m1", module.Id);
        }

        [Fact]
        public async Task CreateModulesRetryTest()
        {
            // Arrange
            var underlying = new Mock<IServiceClient>();
            underlying.SetupSequence(c => c.CreateModules(It.IsAny<IEnumerable<string>>()))
                .Throws(new InvalidOperationException())
                .Returns(Task.FromResult(new[] { new Module("d1", "m1") }));
            var serviceClient = new RetryingServiceClient(underlying.Object);

            // Act
            Module[] modules = await serviceClient.CreateModules(new List<string> { "m1" });

            // Assert
            Assert.NotNull(modules);
            Assert.Single(modules);
            Assert.Equal("m1", modules[0].Id);
        }

        [Fact]
        public async Task GetModuleThrowsTest()
        {
            // Arrange
            var underlying = new Mock<IServiceClient>();
            underlying.Setup(c => c.GetModules())
                .ThrowsAsync(new InvalidOperationException());
            var serviceClient = new RetryingServiceClient(underlying.Object);

            // Act / assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => serviceClient.GetModules());

            // Assert
            underlying.Verify(u => u.GetModules(), Times.Exactly(4));
        }

        [Fact]
        public async Task GetModulesRetryUnauthorizedThrowsTest()
        {
            // Arrange
            var underlying = new Mock<IServiceClient>();
            underlying.SetupSequence(c => c.GetModules())
                .Throws(new UnauthorizedException("Unauthorized!"))
                .Returns(Task.FromResult(Enumerable.Empty<Module>()));
            var serviceClient = new RetryingServiceClient(underlying.Object);

            // Act / Assert
            await Assert.ThrowsAsync<UnauthorizedException>(() => serviceClient.GetModules());
        }
    }
}
