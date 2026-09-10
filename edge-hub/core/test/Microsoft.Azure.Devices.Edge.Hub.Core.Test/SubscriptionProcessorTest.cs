// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Test
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;

    [Unit]
    public class SubscriptionProcessorTest
    {
        public static SubscriptionProcessor GetSubscriptionProcessor(IConnectionManager connectionManager = null)
        {
            // Arrange
            connectionManager = connectionManager ?? Mock.Of<IConnectionManager>();
            var subscriptionProcessor = new SubscriptionProcessor(
                connectionManager,
                Mock.Of<IInvokeMethodHandler>(),
                Mock.Of<IDeviceConnectivityManager>());
            return subscriptionProcessor;
        }

        [Fact]
        public async Task ProcessC2DSubscriptionTest()
        {
            // Arrange
            var milestone = new SemaphoreSlim(0, 1);
            string id = "d1";
            var cloudProxy = new Mock<ICloudProxy>(MockBehavior.Strict);
            cloudProxy.Setup(c => c.StartListening()).Callback(() => milestone.Release());
            var connectionManager = Mock.Of<IConnectionManager>(c => c.GetCloudConnection(id) == Task.FromResult(Option.Some(cloudProxy.Object)));
            Mock.Get(connectionManager).Setup(c => c.AddSubscription(id, DeviceSubscription.C2D)).Returns(true);
            SubscriptionProcessor subscriptionProcessor = GetSubscriptionProcessor(connectionManager);

            // Act
            await subscriptionProcessor.AddSubscription(id, DeviceSubscription.C2D);
            await milestone.WaitAsync(TimeSpan.FromSeconds(5));

            // Assert
            cloudProxy.VerifyAll();

            cloudProxy = new Mock<ICloudProxy>(MockBehavior.Strict);
            connectionManager = Mock.Of<IConnectionManager>(c => c.GetCloudConnection(id) == Task.FromResult(Option.Some(cloudProxy.Object)));
            Mock.Get(connectionManager).Setup(c => c.RemoveSubscription(id, DeviceSubscription.C2D)).Returns(true);
            subscriptionProcessor = GetSubscriptionProcessor(connectionManager);

            // Act
            await subscriptionProcessor.RemoveSubscription(id, DeviceSubscription.C2D);

            // Assert
            cloudProxy.VerifyAll();
        }

        [Fact]
        public async Task ProcessDesiredPropertiesSubscriptionTest()
        {
            // Arrange
            var milestone = new SemaphoreSlim(0, 1);
            string id = "d1";
            var cloudProxy = new Mock<ICloudProxy>(MockBehavior.Strict);
            cloudProxy.Setup(c => c.SetupDesiredPropertyUpdatesAsync()).Callback(() => milestone.Release()).Returns(Task.CompletedTask);
            var connectionManager = Mock.Of<IConnectionManager>(c => c.GetCloudConnection(id) == Task.FromResult(Option.Some(cloudProxy.Object)));
            Mock.Get(connectionManager).Setup(c => c.AddSubscription(id, DeviceSubscription.DesiredPropertyUpdates)).Returns(true);
            SubscriptionProcessor subscriptionProcessor = GetSubscriptionProcessor(connectionManager);

            // Act
            await subscriptionProcessor.AddSubscription(id, DeviceSubscription.DesiredPropertyUpdates);
            await milestone.WaitAsync(TimeSpan.FromSeconds(5));

            // Assert
            cloudProxy.VerifyAll();

            milestone = new SemaphoreSlim(0, 1);
            cloudProxy = new Mock<ICloudProxy>(MockBehavior.Strict);
            cloudProxy.Setup(c => c.RemoveDesiredPropertyUpdatesAsync()).Callback(() => milestone.Release()).Returns(Task.CompletedTask);
            connectionManager = Mock.Of<IConnectionManager>(c => c.GetCloudConnection(id) == Task.FromResult(Option.Some(cloudProxy.Object)));
            Mock.Get(connectionManager).Setup(c => c.RemoveSubscription(id, DeviceSubscription.DesiredPropertyUpdates)).Returns(true);
            subscriptionProcessor = GetSubscriptionProcessor(connectionManager);

            // Act
            await subscriptionProcessor.RemoveSubscription(id, DeviceSubscription.DesiredPropertyUpdates);
            await milestone.WaitAsync(TimeSpan.FromSeconds(5));

            // Assert
            cloudProxy.VerifyAll();
        }

        [Fact]
        public async Task ProcessMethodsSubscriptionTest()
        {
            // Arrange
            var milestone = new SemaphoreSlim(0, 1);
            string id = "d1";
            var cloudProxy = new Mock<ICloudProxy>(MockBehavior.Strict);
            cloudProxy.Setup(c => c.SetupCallMethodAsync()).Callback(() => milestone.Release()).Returns(Task.CompletedTask);
            var connectionManager = Mock.Of<IConnectionManager>(c => c.GetCloudConnection(id) == Task.FromResult(Option.Some(cloudProxy.Object)));
            Mock.Get(connectionManager).Setup(c => c.AddSubscription(id, DeviceSubscription.Methods)).Returns(true);
            SubscriptionProcessor subscriptionProcessor = GetSubscriptionProcessor(connectionManager);

            // Act
            await subscriptionProcessor.AddSubscription(id, DeviceSubscription.Methods);
            await milestone.WaitAsync(TimeSpan.FromSeconds(5));

            // Assert
            cloudProxy.VerifyAll();

            cloudProxy = new Mock<ICloudProxy>(MockBehavior.Strict);
            cloudProxy.Setup(c => c.RemoveCallMethodAsync()).Callback(() => milestone.Release()).Returns(Task.CompletedTask);
            connectionManager = Mock.Of<IConnectionManager>(c => c.GetCloudConnection(id) == Task.FromResult(Option.Some(cloudProxy.Object)));
            Mock.Get(connectionManager).Setup(c => c.RemoveSubscription(id, DeviceSubscription.Methods)).Returns(true);
            subscriptionProcessor = GetSubscriptionProcessor(connectionManager);

            // Act
            await subscriptionProcessor.RemoveSubscription(id, DeviceSubscription.Methods);
            await milestone.WaitAsync(TimeSpan.FromSeconds(5));

            // Assert
            cloudProxy.VerifyAll();
        }

        [Fact]
        public async Task ProcessMultipleSubscriptionsTest()
        {
            // Arrange
            int callCounter = 3;
            var milestone = new SemaphoreSlim(0, 1);
            string id = "d1";
            var cloudProxy = new Mock<ICloudProxy>(MockBehavior.Strict);
            cloudProxy.Setup(c => c.SetupCallMethodAsync()).Callback(Called).Returns(Task.CompletedTask);
            cloudProxy.Setup(c => c.RemoveDesiredPropertyUpdatesAsync()).Callback(Called).Returns(Task.CompletedTask);
            cloudProxy.Setup(c => c.StartListening()).Callback(Called).Returns(Task.CompletedTask);
            var connectionManager = Mock.Of<IConnectionManager>(c => c.GetCloudConnection(id) == Task.FromResult(Option.Some(cloudProxy.Object)));
            SubscriptionProcessor subscriptionProcessor = GetSubscriptionProcessor(connectionManager);

            // Act
            await subscriptionProcessor.ProcessSubscriptions(
                id,
                new[]
                {
                    (DeviceSubscription.C2D, true),
                    (DeviceSubscription.DesiredPropertyUpdates, false),
                    (DeviceSubscription.Methods, true),
                    (DeviceSubscription.TwinResponse, true)
                });

            // Assert
            await milestone.WaitAsync(TimeSpan.FromSeconds(5));
            cloudProxy.VerifyAll();

            void Called()
            {
                if (Interlocked.Decrement(ref callCounter) == 0)
                {
                    milestone.Release();
                }
            }
        }

        [Fact]
        public async Task ProcessMultipleSubscriptionCallsTest()
        {
            // Arrange
            var milestone = new SemaphoreSlim(0, 1);
            string id = "d1";

            async Task<Option<ICloudProxy>> DummyProxyGetter()
            {
                await Task.Delay(TimeSpan.FromSeconds(3));
                throw new TimeoutException();
            }

            var cloudProxy = new Mock<ICloudProxy>(MockBehavior.Strict);
            var connectionManager = new Mock<IConnectionManager>(MockBehavior.Strict);
            connectionManager.Setup(c => c.AddSubscription(id, It.IsAny<DeviceSubscription>())).Returns(true);
            connectionManager.Setup(c => c.GetCloudConnection(id))
                .Callback(() => milestone.Release())
                .Returns(DummyProxyGetter);
            SubscriptionProcessor subscriptionProcessor = GetSubscriptionProcessor(connectionManager.Object);

            // Act
            await subscriptionProcessor.AddSubscription(id, DeviceSubscription.C2D);
            await subscriptionProcessor.AddSubscription(id, DeviceSubscription.DesiredPropertyUpdates);
            await subscriptionProcessor.AddSubscription(id, DeviceSubscription.Methods);
            await subscriptionProcessor.AddSubscription(id, DeviceSubscription.TwinResponse);

            // Assert
            await milestone.WaitAsync(TimeSpan.FromSeconds(5));
            cloudProxy.VerifyAll();
            connectionManager.Verify(c => c.GetCloudConnection(id), Times.Once);
        }

        [Fact]
        public async Task ProcessNoOpSubscriptionTest()
        {
            // Arrange
            string id = "d1";
            var cloudProxy = new Mock<ICloudProxy>(MockBehavior.Strict);
            var connectionManager = Mock.Of<IConnectionManager>(c => c.GetCloudConnection(id) == Task.FromResult(Option.Some(cloudProxy.Object)));
            SubscriptionProcessor subscriptionProcessor = GetSubscriptionProcessor(connectionManager);

            // Act
            await subscriptionProcessor.AddSubscription(id, DeviceSubscription.ModuleMessages);
            await subscriptionProcessor.AddSubscription(id, DeviceSubscription.TwinResponse);
            await subscriptionProcessor.AddSubscription(id, DeviceSubscription.Unknown);

            // Assert
            cloudProxy.VerifyAll();

            cloudProxy = new Mock<ICloudProxy>(MockBehavior.Strict);
            connectionManager = Mock.Of<IConnectionManager>(c => c.GetCloudConnection(id) == Task.FromResult(Option.Some(cloudProxy.Object)));
            subscriptionProcessor = GetSubscriptionProcessor(connectionManager);

            // Act
            await subscriptionProcessor.RemoveSubscription(id, DeviceSubscription.ModuleMessages);
            await subscriptionProcessor.RemoveSubscription(id, DeviceSubscription.TwinResponse);
            await subscriptionProcessor.RemoveSubscription(id, DeviceSubscription.Unknown);

            // Assert
            cloudProxy.VerifyAll();
        }

        [Fact]
        public async Task AddSubscriptionHandlesExceptionTest()
        {
            // Arrange
            var milestone = new SemaphoreSlim(0, 1);
            string deviceId = "d1";
            var cloudProxy = new Mock<ICloudProxy>();
            cloudProxy.Setup(c => c.SetupCallMethodAsync())
                .Callback(() => milestone.Release())
                .ThrowsAsync(new InvalidOperationException());

            var connectionManager = new Mock<IConnectionManager>();
            connectionManager.Setup(c => c.AddSubscription(deviceId, DeviceSubscription.Methods)).Returns(true);
            connectionManager.Setup(c => c.GetCloudConnection(deviceId)).Returns(Task.FromResult(Option.Some(cloudProxy.Object)));
            var subscriptionProcessor = GetSubscriptionProcessor(connectionManager.Object);

            // Act
            await subscriptionProcessor.AddSubscription(deviceId, DeviceSubscription.Methods);
            await milestone.WaitAsync(TimeSpan.FromSeconds(5));

            // Assert
            cloudProxy.VerifyAll();
            connectionManager.VerifyAll();
        }

        [Fact]
        public async Task AddSubscriptionTimesOutTest()
        {
            // Arrange
            var milestone = new SemaphoreSlim(0, 1);
            string deviceId = "d1";
            var cloudProxy = new Mock<ICloudProxy>();
            cloudProxy.Setup(c => c.SetupCallMethodAsync())
                .Callback(() => milestone.Release())
                .ThrowsAsync(new TimeoutException());

            var connectionManager = new Mock<IConnectionManager>();
            connectionManager.Setup(c => c.AddSubscription(deviceId, DeviceSubscription.Methods)).Returns(true);
            connectionManager.Setup(c => c.GetCloudConnection(deviceId)).Returns(Task.FromResult(Option.Some(cloudProxy.Object)));
            var subscriptionProcessor = GetSubscriptionProcessor(connectionManager.Object);

            // Act
            await subscriptionProcessor.AddSubscription(deviceId, DeviceSubscription.Methods);
            await milestone.WaitAsync(TimeSpan.FromSeconds(5));

            // Assert
            cloudProxy.VerifyAll();
            connectionManager.VerifyAll();
        }

        [Fact]
        public async Task RemoveSubscriptionHandlesExceptionTest()
        {
            // Arrange
            var milestone = new SemaphoreSlim(0, 1);
            string deviceId = "d1";
            var cloudProxy = new Mock<ICloudProxy>();
            cloudProxy.Setup(c => c.RemoveCallMethodAsync())
                .Callback(() => milestone.Release())
                .ThrowsAsync(new InvalidOperationException());

            var connectionManager = new Mock<IConnectionManager>();
            connectionManager.Setup(c => c.RemoveSubscription(deviceId, DeviceSubscription.Methods)).Returns(true);
            connectionManager.Setup(c => c.GetCloudConnection(deviceId)).Returns(Task.FromResult(Option.Some(cloudProxy.Object)));
            var subscriptionProcessor = GetSubscriptionProcessor(connectionManager.Object);

            // Act
            await subscriptionProcessor.RemoveSubscription(deviceId, DeviceSubscription.Methods);
            await milestone.WaitAsync(TimeSpan.FromSeconds(5));

            // Assert
            cloudProxy.VerifyAll();
            connectionManager.VerifyAll();
        }

        [Fact]
        public async Task RemoveSubscriptionTimesOutTest()
        {
            // Arrange
            var milestone = new SemaphoreSlim(0, 1);
            string deviceId = "d1";
            var cloudProxy = new Mock<ICloudProxy>();
            cloudProxy.Setup(c => c.RemoveCallMethodAsync())
                .Callback(() => milestone.Release())
                .ThrowsAsync(new TimeoutException());

            var connectionManager = new Mock<IConnectionManager>();
            connectionManager.Setup(c => c.RemoveSubscription(deviceId, DeviceSubscription.Methods)).Returns(true);
            connectionManager.Setup(c => c.GetCloudConnection(deviceId)).Returns(Task.FromResult(Option.Some(cloudProxy.Object)));
            var subscriptionProcessor = GetSubscriptionProcessor(connectionManager.Object);

            // Act
            await subscriptionProcessor.RemoveSubscription(deviceId, DeviceSubscription.Methods);
            await milestone.WaitAsync(TimeSpan.FromSeconds(5));

            // Assert
            cloudProxy.VerifyAll();
            connectionManager.VerifyAll();
        }

        [Fact]
        public void ProcessSubscriptionsOnDeviceConnected()
        {
            // Arrange
            string d1 = "d1";
            var deviceIdentity = Mock.Of<IIdentity>(d => d.Id == d1);
            string m1 = "d2/m1";
            var moduleIdentity = Mock.Of<IIdentity>(m => m.Id == m1);

            var connectedClients = new List<IIdentity>
            {
                deviceIdentity,
                moduleIdentity
            };

            IReadOnlyDictionary<DeviceSubscription, bool> device1Subscriptions = new Dictionary<DeviceSubscription, bool>()
            {
                [DeviceSubscription.Methods] = true,
                [DeviceSubscription.DesiredPropertyUpdates] = true
            };

            IReadOnlyDictionary<DeviceSubscription, bool> module1Subscriptions = new Dictionary<DeviceSubscription, bool>()
            {
                [DeviceSubscription.Methods] = true,
                [DeviceSubscription.ModuleMessages] = true
            };

            var device1CloudProxy = Mock.Of<ICloudProxy>(
                dc => dc.SetupDesiredPropertyUpdatesAsync() == Task.CompletedTask
                      && dc.SetupCallMethodAsync() == Task.CompletedTask);
            Mock.Get(device1CloudProxy).SetupGet(d => d.IsActive).Returns(true);
            var module1CloudProxy = Mock.Of<ICloudProxy>(mc => mc.SetupCallMethodAsync() == Task.CompletedTask && mc.IsActive);

            var invokeMethodHandler = Mock.Of<IInvokeMethodHandler>(
                m =>
                    m.ProcessInvokeMethodSubscription(d1) == Task.CompletedTask
                    && m.ProcessInvokeMethodSubscription(m1) == Task.CompletedTask);

            var connectionManager = Mock.Of<IConnectionManager>(
                c =>
                    c.GetConnectedClients() == connectedClients
                    && c.GetSubscriptions(d1) == Option.Some(device1Subscriptions)
                    && c.GetSubscriptions(m1) == Option.Some(module1Subscriptions)
                    && c.GetCloudConnection(d1) == Task.FromResult(Option.Some(device1CloudProxy))
                    && c.GetCloudConnection(m1) == Task.FromResult(Option.Some(module1CloudProxy)));

            var deviceConnectivityManager = Mock.Of<IDeviceConnectivityManager>();

            var subscriptionProcessor = new SubscriptionProcessor(connectionManager, invokeMethodHandler, deviceConnectivityManager);

            // Act
            Mock.Get(deviceConnectivityManager).Raise(d => d.DeviceConnected += null, new EventArgs());

            // Assert
            Mock.Get(device1CloudProxy).Verify(d => d.SetupDesiredPropertyUpdatesAsync(), Times.Once);
            Mock.Get(device1CloudProxy).Verify(d => d.SetupCallMethodAsync(), Times.Once);
            Mock.Get(module1CloudProxy).Verify(m => m.SetupCallMethodAsync(), Times.Once);
            Mock.Get(invokeMethodHandler).VerifyAll();
            Mock.Get(connectionManager).VerifyAll();
        }

        [Fact]
        public void ProcessSubscriptionsOnDeviceConnectedWithGetCloudConnectionTimeout()
        {
            // Arrange
            string d1 = "d1";
            var deviceIdentity = Mock.Of<IIdentity>(d => d.Id == d1);
            string m1 = "d2/m1";
            var moduleIdentity = Mock.Of<IIdentity>(m => m.Id == m1);

            var connectedClients = new List<IIdentity>
            {
                deviceIdentity,
                moduleIdentity
            };

            IReadOnlyDictionary<DeviceSubscription, bool> device1Subscriptions = new Dictionary<DeviceSubscription, bool>()
            {
                [DeviceSubscription.Methods] = true,
                [DeviceSubscription.DesiredPropertyUpdates] = true
            };

            IReadOnlyDictionary<DeviceSubscription, bool> module1Subscriptions = new Dictionary<DeviceSubscription, bool>()
            {
                [DeviceSubscription.Methods] = true,
                [DeviceSubscription.ModuleMessages] = true
            };

            var device1CloudProxy = Mock.Of<ICloudProxy>(
                dc => dc.SetupDesiredPropertyUpdatesAsync() == Task.CompletedTask
                      && dc.SetupCallMethodAsync() == Task.CompletedTask);
            Mock.Get(device1CloudProxy).SetupGet(d => d.IsActive).Returns(true);
            var module1CloudProxy = Mock.Of<ICloudProxy>(mc => mc.SetupCallMethodAsync() == Task.CompletedTask && mc.IsActive);

            var invokeMethodHandler = Mock.Of<IInvokeMethodHandler>(
                m => m.ProcessInvokeMethodSubscription(m1) == Task.CompletedTask);

            var connectionManager = Mock.Of<IConnectionManager>(
                c =>
                    c.GetConnectedClients() == connectedClients
                    && c.GetSubscriptions(m1) == Option.Some(module1Subscriptions)
                    && c.GetCloudConnection(m1) == Task.FromResult(Option.Some(module1CloudProxy)));

            Mock.Get(connectionManager).Setup(c => c.GetCloudConnection(d1)).Throws(new TimeoutException("Test GetCloudConnection Timeout"));

            var deviceConnectivityManager = Mock.Of<IDeviceConnectivityManager>();

            var subscriptionProcessor = new SubscriptionProcessor(connectionManager, invokeMethodHandler, deviceConnectivityManager);

            // Act
            Mock.Get(deviceConnectivityManager).Raise(d => d.DeviceConnected += null, new EventArgs());

            // Assert
            Mock.Get(device1CloudProxy).Verify(d => d.SetupDesiredPropertyUpdatesAsync(), Times.Never);
            Mock.Get(device1CloudProxy).Verify(d => d.SetupCallMethodAsync(), Times.Never);
            Mock.Get(module1CloudProxy).Verify(m => m.SetupCallMethodAsync(), Times.Once);
            Mock.Get(invokeMethodHandler).VerifyAll();
            Mock.Get(connectionManager).VerifyAll();
        }

        [Fact]
        public void ProcessSubscriptionsOnDeviceConnectedWithProcessInvokeMethodSubscriptionException()
        {
            // Arrange
            string d1 = "d1";
            var deviceIdentity = Mock.Of<IIdentity>(d => d.Id == d1);
            string m1 = "d2/m1";
            var moduleIdentity = Mock.Of<IIdentity>(m => m.Id == m1);

            var connectedClients = new List<IIdentity>
            {
                deviceIdentity,
                moduleIdentity
            };

            IReadOnlyDictionary<DeviceSubscription, bool> device1Subscriptions = new Dictionary<DeviceSubscription, bool>()
            {
                [DeviceSubscription.Methods] = true,
                [DeviceSubscription.DesiredPropertyUpdates] = true
            };

            IReadOnlyDictionary<DeviceSubscription, bool> module1Subscriptions = new Dictionary<DeviceSubscription, bool>()
            {
                [DeviceSubscription.Methods] = true,
                [DeviceSubscription.ModuleMessages] = true
            };

            var device1CloudProxy = Mock.Of<ICloudProxy>(
                dc => dc.SetupDesiredPropertyUpdatesAsync() == Task.CompletedTask
                      && dc.SetupCallMethodAsync() == Task.CompletedTask);
            Mock.Get(device1CloudProxy).SetupGet(d => d.IsActive).Returns(true);
            var module1CloudProxy = Mock.Of<ICloudProxy>(mc => mc.SetupCallMethodAsync() == Task.CompletedTask && mc.IsActive);

            var invokeMethodHandler = Mock.Of<IInvokeMethodHandler>(
                m =>
                    m.ProcessInvokeMethodSubscription(d1) == Task.CompletedTask);

            Mock.Get(invokeMethodHandler).Setup(m => m.ProcessInvokeMethodSubscription(m1)).Throws(new TimeoutException("Test ProcessInvokeMethodSubscription timeout"));

            var connectionManager = Mock.Of<IConnectionManager>(
                c =>
                    c.GetConnectedClients() == connectedClients
                    && c.GetSubscriptions(d1) == Option.Some(device1Subscriptions)
                    && c.GetSubscriptions(m1) == Option.Some(module1Subscriptions)
                    && c.GetCloudConnection(d1) == Task.FromResult(Option.Some(device1CloudProxy))
                    && c.GetCloudConnection(m1) == Task.FromResult(Option.Some(module1CloudProxy)));

            var deviceConnectivityManager = Mock.Of<IDeviceConnectivityManager>();

            var subscriptionProcessor = new SubscriptionProcessor(connectionManager, invokeMethodHandler, deviceConnectivityManager);

            // Act
            Mock.Get(deviceConnectivityManager).Raise(d => d.DeviceConnected += null, new EventArgs());

            // Assert
            Mock.Get(device1CloudProxy).Verify(d => d.SetupDesiredPropertyUpdatesAsync(), Times.Once);
            Mock.Get(device1CloudProxy).Verify(d => d.SetupCallMethodAsync(), Times.Once);
            Mock.Get(module1CloudProxy).Verify(m => m.SetupCallMethodAsync(), Times.Exactly(2));
            Mock.Get(invokeMethodHandler).VerifyAll();
            Mock.Get(connectionManager).VerifyAll();
        }

        [Fact]
        public void ProcessSubscriptionsOnClientCloudConnectionEstablished()
        {
            // Arrange
            string d1 = "d1";
            var deviceIdentity = Mock.Of<IIdentity>(d => d.Id == d1);
            string m1 = "d2/m1";
            var moduleIdentity = Mock.Of<IIdentity>(m => m.Id == m1);

            IReadOnlyDictionary<DeviceSubscription, bool> device1Subscriptions = new Dictionary<DeviceSubscription, bool>()
            {
                [DeviceSubscription.Methods] = true,
                [DeviceSubscription.DesiredPropertyUpdates] = true
            };

            IReadOnlyDictionary<DeviceSubscription, bool> module1Subscriptions = new Dictionary<DeviceSubscription, bool>()
            {
                [DeviceSubscription.Methods] = true,
                [DeviceSubscription.ModuleMessages] = true
            };

            var device1CloudProxy = Mock.Of<ICloudProxy>(
                dc => dc.SetupDesiredPropertyUpdatesAsync() == Task.CompletedTask
                      && dc.SetupCallMethodAsync() == Task.CompletedTask);
            Mock.Get(device1CloudProxy).SetupGet(d => d.IsActive).Returns(true);
            var module1CloudProxy = Mock.Of<ICloudProxy>(mc => mc.SetupCallMethodAsync() == Task.CompletedTask && mc.IsActive);

            var invokeMethodHandler = Mock.Of<IInvokeMethodHandler>(
                m =>
                    m.ProcessInvokeMethodSubscription(d1) == Task.CompletedTask
                    && m.ProcessInvokeMethodSubscription(m1) == Task.CompletedTask);

            var connectionManager = Mock.Of<IConnectionManager>(
                c =>
                    c.GetSubscriptions(d1) == Option.Some(device1Subscriptions)
                    && c.GetSubscriptions(m1) == Option.Some(module1Subscriptions)
                    && c.GetCloudConnection(d1) == Task.FromResult(Option.Some(device1CloudProxy))
                    && c.GetCloudConnection(m1) == Task.FromResult(Option.Some(module1CloudProxy)));

            var deviceConnectivityManager = Mock.Of<IDeviceConnectivityManager>();

            var subscriptionProcessor = new SubscriptionProcessor(connectionManager, invokeMethodHandler, deviceConnectivityManager);

            // Act
            Mock.Get(connectionManager).Raise(d => d.CloudConnectionEstablished += null, this, deviceIdentity);

            // Assert
            Mock.Get(device1CloudProxy).Verify(d => d.SetupDesiredPropertyUpdatesAsync(), Times.Once);
            Mock.Get(device1CloudProxy).Verify(d => d.SetupCallMethodAsync(), Times.Once);
            Mock.Get(module1CloudProxy).Verify(m => m.SetupCallMethodAsync(), Times.Never);

            // Act
            Mock.Get(connectionManager).Raise(d => d.CloudConnectionEstablished += null, this, moduleIdentity);

            // Assert
            Mock.Get(device1CloudProxy).Verify(d => d.SetupDesiredPropertyUpdatesAsync(), Times.Once);
            Mock.Get(device1CloudProxy).Verify(d => d.SetupCallMethodAsync(), Times.Once);
            Mock.Get(module1CloudProxy).Verify(m => m.SetupCallMethodAsync(), Times.Once);

            Mock.Get(invokeMethodHandler).VerifyAll();
            Mock.Get(connectionManager).VerifyAll();
        }

        [Fact]
        public async Task PendingMethodsSubscriptionSurvivesMissingCloudProxy()
        {
            string id = "d1/m1";
            bool proxyAvailable = false;
            var firstAttempt = new SemaphoreSlim(0);
            var subscriptionApplied = new SemaphoreSlim(0, 1);
            var cloudProxy = new Mock<ICloudProxy>(MockBehavior.Strict);
            cloudProxy.Setup(c => c.SetupCallMethodAsync())
                .Callback(() => subscriptionApplied.Release())
                .Returns(Task.CompletedTask);
            var connectionManager = new Mock<IConnectionManager>();
            connectionManager.Setup(c => c.AddSubscription(id, DeviceSubscription.Methods)).Returns(true);
            connectionManager.Setup(c => c.GetDeviceConnection(id)).Returns(Option.Some(Mock.Of<IDeviceProxy>()));
            connectionManager.Setup(c => c.GetCloudConnection(id))
                .Returns(
                    () =>
                    {
                        if (!proxyAvailable)
                        {
                            firstAttempt.Release();
                            return Task.FromResult(Option.None<ICloudProxy>());
                        }

                        return Task.FromResult(Option.Some(cloudProxy.Object));
                    });
            var invokeMethodHandler = Mock.Of<IInvokeMethodHandler>(
                h => h.ProcessInvokeMethodSubscription(id) == Task.CompletedTask);
            using var subscriptionProcessor = new SubscriptionProcessor(
                connectionManager.Object,
                invokeMethodHandler,
                Mock.Of<IDeviceConnectivityManager>());

            await subscriptionProcessor.AddSubscription(id, DeviceSubscription.Methods);
            Assert.True(await firstAttempt.WaitAsync(TimeSpan.FromSeconds(5)));
            cloudProxy.Verify(c => c.SetupCallMethodAsync(), Times.Never);

            proxyAvailable = true;
            Assert.True(await subscriptionApplied.WaitAsync(TimeSpan.FromSeconds(5)));

            cloudProxy.Verify(c => c.SetupCallMethodAsync(), Times.Once);
            Mock.Get(invokeMethodHandler).Verify(
                h => h.ProcessInvokeMethodSubscription(id),
                Times.Once);
        }

        [Fact]
        public async Task PendingMethodsSubscriptionRecoversAfterCloudProxyCreationThrows()
        {
            string id = "d1/m1";
            var subscriptionApplied = new SemaphoreSlim(0, 1);
            var cloudProxy = new Mock<ICloudProxy>(MockBehavior.Strict);
            cloudProxy.Setup(c => c.SetupCallMethodAsync())
                .Callback(() => subscriptionApplied.Release())
                .Returns(Task.CompletedTask);
            var connectionManager = new Mock<IConnectionManager>();
            connectionManager.Setup(c => c.AddSubscription(id, DeviceSubscription.Methods)).Returns(true);
            connectionManager.Setup(c => c.GetDeviceConnection(id)).Returns(Option.Some(Mock.Of<IDeviceProxy>()));
            connectionManager.SetupSequence(c => c.GetCloudConnection(id))
                .ThrowsAsync(new TimeoutException())
                .ReturnsAsync(Option.Some(cloudProxy.Object));
            using var subscriptionProcessor = new SubscriptionProcessor(
                connectionManager.Object,
                Mock.Of<IInvokeMethodHandler>(
                    h => h.ProcessInvokeMethodSubscription(id) == Task.CompletedTask),
                Mock.Of<IDeviceConnectivityManager>());

            await subscriptionProcessor.AddSubscription(id, DeviceSubscription.Methods);

            Assert.True(await subscriptionApplied.WaitAsync(TimeSpan.FromSeconds(5)));
            cloudProxy.Verify(c => c.SetupCallMethodAsync(), Times.Once);
            connectionManager.Verify(c => c.GetCloudConnection(id), Times.Exactly(2));
        }

        [Fact]
        public async Task TransientSubscriptionFailureStillRetries()
        {
            string id = "d1/m1";
            int setupAttempts = 0;
            var subscriptionApplied = new SemaphoreSlim(0, 1);
            var cloudProxy = new Mock<ICloudProxy>(MockBehavior.Strict);
            cloudProxy.Setup(c => c.SetupCallMethodAsync())
                .Returns(
                    () =>
                    {
                        if (Interlocked.Increment(ref setupAttempts) == 1)
                        {
                            return Task.FromException(new InvalidOperationException());
                        }

                        subscriptionApplied.Release();
                        return Task.CompletedTask;
                    });
            var connectionManager = new Mock<IConnectionManager>();
            connectionManager.Setup(c => c.AddSubscription(id, DeviceSubscription.Methods)).Returns(true);
            connectionManager.Setup(c => c.GetCloudConnection(id)).ReturnsAsync(Option.Some(cloudProxy.Object));
            using var subscriptionProcessor = new SubscriptionProcessor(
                connectionManager.Object,
                Mock.Of<IInvokeMethodHandler>(
                    h => h.ProcessInvokeMethodSubscription(id) == Task.CompletedTask),
                Mock.Of<IDeviceConnectivityManager>());

            await subscriptionProcessor.AddSubscription(id, DeviceSubscription.Methods);

            Assert.True(await subscriptionApplied.WaitAsync(TimeSpan.FromSeconds(5)));
            cloudProxy.Verify(c => c.SetupCallMethodAsync(), Times.Exactly(2));
        }

        [Fact]
        public async Task PendingSubscriptionsPreserveAddRemoveOrderWhileCloudProxyIsMissing()
        {
            string id = "d1/m1";
            bool proxyAvailable = false;
            var firstAttempt = new SemaphoreSlim(0);
            var subscriptionsApplied = new SemaphoreSlim(0, 1);
            var cloudProxy = new Mock<ICloudProxy>(MockBehavior.Strict);
            var sequence = new MockSequence();
            cloudProxy.InSequence(sequence)
                .Setup(c => c.SetupCallMethodAsync())
                .Returns(Task.CompletedTask);
            cloudProxy.InSequence(sequence)
                .Setup(c => c.RemoveCallMethodAsync())
                .Callback(() => subscriptionsApplied.Release())
                .Returns(Task.CompletedTask);
            var connectionManager = new Mock<IConnectionManager>();
            connectionManager.Setup(c => c.AddSubscription(id, DeviceSubscription.Methods)).Returns(true);
            connectionManager.Setup(c => c.RemoveSubscription(id, DeviceSubscription.Methods)).Returns(true);
            connectionManager.Setup(c => c.GetDeviceConnection(id)).Returns(Option.Some(Mock.Of<IDeviceProxy>()));
            connectionManager.Setup(c => c.GetCloudConnection(id))
                .Returns(
                    () =>
                    {
                        if (!proxyAvailable)
                        {
                            firstAttempt.Release();
                            return Task.FromResult(Option.None<ICloudProxy>());
                        }

                        return Task.FromResult(Option.Some(cloudProxy.Object));
                    });
            using var subscriptionProcessor = new SubscriptionProcessor(
                connectionManager.Object,
                Mock.Of<IInvokeMethodHandler>(
                    h => h.ProcessInvokeMethodSubscription(id) == Task.CompletedTask),
                Mock.Of<IDeviceConnectivityManager>());

            await subscriptionProcessor.AddSubscription(id, DeviceSubscription.Methods);
            await subscriptionProcessor.RemoveSubscription(id, DeviceSubscription.Methods);
            Assert.True(await firstAttempt.WaitAsync(TimeSpan.FromSeconds(5)));

            proxyAvailable = true;
            Assert.True(await subscriptionsApplied.WaitAsync(TimeSpan.FromSeconds(5)));

            cloudProxy.VerifyAll();
        }

        [Fact]
        public async Task MissingCloudProxyForOneClientDoesNotDelayAnotherClient()
        {
            string unavailableId = "d1/m1";
            string availableId = "d1/m2";
            var unavailableProxyResult = new TaskCompletionSource<Option<ICloudProxy>>(TaskCreationOptions.RunContinuationsAsynchronously);
            var subscriptionApplied = new SemaphoreSlim(0, 1);
            var cloudProxy = new Mock<ICloudProxy>(MockBehavior.Strict);
            cloudProxy.Setup(c => c.SetupDesiredPropertyUpdatesAsync())
                .Callback(() => subscriptionApplied.Release())
                .Returns(Task.CompletedTask);
            var connectionManager = new Mock<IConnectionManager>();
            connectionManager.Setup(c => c.AddSubscription(It.IsAny<string>(), It.IsAny<DeviceSubscription>())).Returns(true);
            connectionManager.Setup(c => c.GetCloudConnection(unavailableId))
                .Returns(unavailableProxyResult.Task);
            connectionManager.Setup(c => c.GetCloudConnection(availableId))
                .ReturnsAsync(Option.Some(cloudProxy.Object));
            using var subscriptionProcessor = new SubscriptionProcessor(
                connectionManager.Object,
                Mock.Of<IInvokeMethodHandler>(),
                Mock.Of<IDeviceConnectivityManager>());

            await subscriptionProcessor.AddSubscription(unavailableId, DeviceSubscription.Methods);
            await subscriptionProcessor.AddSubscription(availableId, DeviceSubscription.DesiredPropertyUpdates);

            Assert.True(await subscriptionApplied.WaitAsync(TimeSpan.FromSeconds(5)));
            unavailableProxyResult.SetResult(Option.None<ICloudProxy>());
            cloudProxy.VerifyAll();
        }

        [Fact]
        public async Task RepeatedRecoveryEventsCoalesceIntoOneAdditionalPass()
        {
            string id = "d1/m1";
            var identity = Mock.Of<IIdentity>(i => i.Id == id);
            var firstProxyResult = new TaskCompletionSource<Option<ICloudProxy>>(TaskCreationOptions.RunContinuationsAsynchronously);
            var secondPassCompleted = new SemaphoreSlim(0, 1);
            int subscriptionCallCount = 0;
            IReadOnlyDictionary<DeviceSubscription, bool> subscriptions =
                new Dictionary<DeviceSubscription, bool> { [DeviceSubscription.Methods] = true };
            var cloudProxy = new Mock<ICloudProxy>();
            cloudProxy.Setup(c => c.SetupCallMethodAsync())
                .Callback(
                    () =>
                    {
                        if (Interlocked.Increment(ref subscriptionCallCount) == 2)
                        {
                            secondPassCompleted.Release();
                        }
                    })
                .Returns(Task.CompletedTask);
            var connectionManager = new Mock<IConnectionManager>();
            connectionManager.Setup(c => c.GetCloudConnection(id))
                .Returns(firstProxyResult.Task);
            connectionManager.Setup(c => c.GetSubscriptions(id)).Returns(Option.Some(subscriptions));
            using var subscriptionProcessor = new SubscriptionProcessor(
                connectionManager.Object,
                Mock.Of<IInvokeMethodHandler>(
                    h => h.ProcessInvokeMethodSubscription(id) == Task.CompletedTask),
                Mock.Of<IDeviceConnectivityManager>());

            Mock.Get(connectionManager.Object).Raise(c => c.DeviceConnected += null, this, identity);
            Mock.Get(connectionManager.Object).Raise(c => c.CloudConnectionEstablished += null, this, identity);
            Mock.Get(connectionManager.Object).Raise(c => c.CloudConnectionEstablished += null, this, identity);
            firstProxyResult.SetResult(Option.Some(cloudProxy.Object));

            Assert.True(await secondPassCompleted.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.Equal(2, Volatile.Read(ref subscriptionCallCount));
            connectionManager.Verify(c => c.GetCloudConnection(id), Times.Exactly(2));
        }

        [Fact]
        public async Task ReplayRequestedDuringActiveReplayRunsAnotherPass()
        {
            string id = "d1/m1";
            var identity = Mock.Of<IIdentity>(i => i.Id == id);
            var firstReplayStarted = new SemaphoreSlim(0, 1);
            var releaseFirstReplay = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var secondReplayCompleted = new SemaphoreSlim(0, 1);
            IReadOnlyDictionary<DeviceSubscription, bool> subscriptions =
                new Dictionary<DeviceSubscription, bool> { [DeviceSubscription.Methods] = true };
            var cloudProxy = new Mock<ICloudProxy>(MockBehavior.Strict);
            cloudProxy.Setup(c => c.SetupCallMethodAsync())
                .Callback(() => firstReplayStarted.Release())
                .Returns(() => releaseFirstReplay.Task);
            cloudProxy.Setup(c => c.RemoveCallMethodAsync())
                .Callback(() => secondReplayCompleted.Release())
                .Returns(Task.CompletedTask);
            var connectionManager = new Mock<IConnectionManager>();
            connectionManager.Setup(c => c.GetCloudConnection(id))
                .ReturnsAsync(Option.Some(cloudProxy.Object));
            connectionManager.Setup(c => c.GetSubscriptions(id))
                .Returns(() => Option.Some(subscriptions));
            using var subscriptionProcessor = new SubscriptionProcessor(
                connectionManager.Object,
                Mock.Of<IInvokeMethodHandler>(
                    h => h.ProcessInvokeMethodSubscription(id) == Task.CompletedTask),
                Mock.Of<IDeviceConnectivityManager>());

            Mock.Get(connectionManager.Object).Raise(c => c.DeviceConnected += null, this, identity);
            Assert.True(await firstReplayStarted.WaitAsync(TimeSpan.FromSeconds(5)));

            subscriptions = new Dictionary<DeviceSubscription, bool> { [DeviceSubscription.Methods] = false };
            Mock.Get(connectionManager.Object).Raise(c => c.CloudConnectionEstablished += null, this, identity);
            releaseFirstReplay.SetResult(true);

            Assert.True(await secondReplayCompleted.WaitAsync(TimeSpan.FromSeconds(5)));
            cloudProxy.Verify(c => c.SetupCallMethodAsync(), Times.Once);
            cloudProxy.Verify(c => c.RemoveCallMethodAsync(), Times.Once);
        }

        [Fact]
        public async Task ClientInRecoveryDelayDoesNotBlockAnotherClient()
        {
            string unavailableId = "d1/m1";
            string availableId = "d1/m2";
            var recoveryStarted = new SemaphoreSlim(0, 1);
            var subscriptionApplied = new SemaphoreSlim(0, 1);
            var availableCloudProxy = new Mock<ICloudProxy>();
            availableCloudProxy.Setup(c => c.SetupDesiredPropertyUpdatesAsync())
                .Callback(() => subscriptionApplied.Release())
                .Returns(Task.CompletedTask);
            var connectionManager = new Mock<IConnectionManager>();
            connectionManager.Setup(c => c.AddSubscription(It.IsAny<string>(), It.IsAny<DeviceSubscription>())).Returns(true);
            connectionManager.Setup(c => c.GetCloudConnection(unavailableId))
                .Callback(() => recoveryStarted.Release())
                .ReturnsAsync(Option.None<ICloudProxy>());
            connectionManager.Setup(c => c.GetDeviceConnection(unavailableId)).Returns(Option.Some(Mock.Of<IDeviceProxy>()));
            connectionManager.Setup(c => c.GetCloudConnection(availableId)).ReturnsAsync(Option.Some(availableCloudProxy.Object));
            using var subscriptionProcessor = new SubscriptionProcessor(
                connectionManager.Object,
                Mock.Of<IInvokeMethodHandler>(),
                Mock.Of<IDeviceConnectivityManager>());

            await subscriptionProcessor.AddSubscription(unavailableId, DeviceSubscription.Methods);
            Assert.True(await recoveryStarted.WaitAsync(TimeSpan.FromSeconds(5)));
            await subscriptionProcessor.AddSubscription(availableId, DeviceSubscription.DesiredPropertyUpdates);

            Assert.True(await subscriptionApplied.WaitAsync(TimeSpan.FromSeconds(5)));
        }

        [Fact]
        public async Task DisposeCancelsRecoveryDelay()
        {
            string id = "d1/m1";
            var recoveryStarted = new SemaphoreSlim(0, 1);
            var connectionManager = new Mock<IConnectionManager>();
            connectionManager.Setup(c => c.AddSubscription(id, DeviceSubscription.Methods)).Returns(true);
            connectionManager.Setup(c => c.GetCloudConnection(id))
                .Callback(() => recoveryStarted.Release())
                .ReturnsAsync(Option.None<ICloudProxy>());
            connectionManager.Setup(c => c.GetDeviceConnection(id)).Returns(Option.Some(Mock.Of<IDeviceProxy>()));
            var subscriptionProcessor = new SubscriptionProcessor(
                connectionManager.Object,
                Mock.Of<IInvokeMethodHandler>(),
                Mock.Of<IDeviceConnectivityManager>());

            await subscriptionProcessor.AddSubscription(id, DeviceSubscription.Methods);
            Assert.True(await recoveryStarted.WaitAsync(TimeSpan.FromSeconds(5)));
            subscriptionProcessor.Dispose();
            await Task.Delay(TimeSpan.FromMilliseconds(1200));

            connectionManager.Verify(c => c.GetCloudConnection(id), Times.Once);
        }

        [Fact]
        public async Task DisposeCancelsPendingCloudConnectionLookup()
        {
            string id = "d1/m1";
            var cloudLookupStarted = new SemaphoreSlim(0, 1);
            var cloudLookup = new TaskCompletionSource<Option<ICloudProxy>>(TaskCreationOptions.RunContinuationsAsynchronously);
            var cloudProxy = new Mock<ICloudProxy>();
            var connectionManager = new Mock<IConnectionManager>();
            connectionManager.Setup(c => c.AddSubscription(id, DeviceSubscription.Methods)).Returns(true);
            connectionManager.Setup(c => c.GetCloudConnection(id))
                .Callback(() => cloudLookupStarted.Release())
                .Returns(cloudLookup.Task);
            var subscriptionProcessor = new SubscriptionProcessor(
                connectionManager.Object,
                Mock.Of<IInvokeMethodHandler>(),
                Mock.Of<IDeviceConnectivityManager>());

            await subscriptionProcessor.AddSubscription(id, DeviceSubscription.Methods);
            Assert.True(await cloudLookupStarted.WaitAsync(TimeSpan.FromSeconds(5)));

            subscriptionProcessor.Dispose();
            cloudLookup.SetResult(Option.Some(cloudProxy.Object));
            await Task.Delay(TimeSpan.FromMilliseconds(100));

            cloudProxy.Verify(c => c.SetupCallMethodAsync(), Times.Never);
        }
    }
}
