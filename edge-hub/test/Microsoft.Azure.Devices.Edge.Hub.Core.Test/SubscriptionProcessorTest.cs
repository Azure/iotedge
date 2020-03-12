// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Test
{
    using System;
    using System.Collections.Generic;
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
            string id = "d1";
            var cloudProxy = new Mock<ICloudProxy>(MockBehavior.Strict);
            cloudProxy.Setup(c => c.StartListening());
            var connectionManager = Mock.Of<IConnectionManager>(c => c.GetCloudConnection(id) == Task.FromResult(Option.Some(cloudProxy.Object)));
            SubscriptionProcessor subscriptionProcessor = GetSubscriptionProcessor(connectionManager);

            // Act
            await subscriptionProcessor.AddSubscription(id, DeviceSubscription.C2D);

            // Assert
            await Task.Delay(TimeSpan.FromSeconds(5));
            cloudProxy.VerifyAll();

            cloudProxy = new Mock<ICloudProxy>(MockBehavior.Strict);
            connectionManager = Mock.Of<IConnectionManager>(c => c.GetCloudConnection(id) == Task.FromResult(Option.Some(cloudProxy.Object)));
            subscriptionProcessor = GetSubscriptionProcessor(connectionManager);

            // Act
            await subscriptionProcessor.RemoveSubscription(id, DeviceSubscription.C2D);

            // Assert
            await Task.Delay(TimeSpan.FromSeconds(5));
            cloudProxy.VerifyAll();
        }

        [Fact]
        public async Task ProcessDesiredPropertiesSubscriptionTest()
        {
            // Arrange
            string id = "d1";
            var cloudProxy = new Mock<ICloudProxy>(MockBehavior.Strict);
            cloudProxy.Setup(c => c.SetupDesiredPropertyUpdatesAsync());
            var connectionManager = Mock.Of<IConnectionManager>(c => c.GetCloudConnection(id) == Task.FromResult(Option.Some(cloudProxy.Object)));
            SubscriptionProcessor subscriptionProcessor = GetSubscriptionProcessor(connectionManager);

            // Act
            await subscriptionProcessor.AddSubscription(id, DeviceSubscription.DesiredPropertyUpdates);

            // Assert
            await Task.Delay(TimeSpan.FromSeconds(5));
            cloudProxy.VerifyAll();

            cloudProxy = new Mock<ICloudProxy>(MockBehavior.Strict);
            cloudProxy.Setup(c => c.RemoveDesiredPropertyUpdatesAsync());
            connectionManager = Mock.Of<IConnectionManager>(c => c.GetCloudConnection(id) == Task.FromResult(Option.Some(cloudProxy.Object)));
            subscriptionProcessor = GetSubscriptionProcessor(connectionManager);

            // Act
            await subscriptionProcessor.RemoveSubscription(id, DeviceSubscription.DesiredPropertyUpdates);

            // Assert
            await Task.Delay(TimeSpan.FromSeconds(5));
            cloudProxy.VerifyAll();
        }

        [Fact]
        public async Task ProcessMethodsSubscriptionTest()
        {
            // Arrange
            string id = "d1";
            var cloudProxy = new Mock<ICloudProxy>(MockBehavior.Strict);
            cloudProxy.Setup(c => c.SetupCallMethodAsync());
            var connectionManager = Mock.Of<IConnectionManager>(c => c.GetCloudConnection(id) == Task.FromResult(Option.Some(cloudProxy.Object)));
            SubscriptionProcessor subscriptionProcessor = GetSubscriptionProcessor(connectionManager);

            // Act
            await subscriptionProcessor.AddSubscription(id, DeviceSubscription.Methods);

            // Assert
            await Task.Delay(TimeSpan.FromSeconds(5));
            cloudProxy.VerifyAll();

            cloudProxy = new Mock<ICloudProxy>(MockBehavior.Strict);
            cloudProxy.Setup(c => c.RemoveCallMethodAsync());
            connectionManager = Mock.Of<IConnectionManager>(c => c.GetCloudConnection(id) == Task.FromResult(Option.Some(cloudProxy.Object)));
            subscriptionProcessor = GetSubscriptionProcessor(connectionManager);

            // Act
            await subscriptionProcessor.RemoveSubscription(id, DeviceSubscription.Methods);

            // Assert
            await Task.Delay(TimeSpan.FromSeconds(5));
            cloudProxy.VerifyAll();
        }

        [Fact]
        public async Task ProcessMultipleSubscriptionsTest()
        {
            // Arrange
            string id = "d1";
            var cloudProxy = new Mock<ICloudProxy>(MockBehavior.Strict);
            cloudProxy.Setup(c => c.SetupCallMethodAsync());
            cloudProxy.Setup(c => c.RemoveDesiredPropertyUpdatesAsync());
            cloudProxy.Setup(c => c.StartListening());
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
            await Task.Delay(TimeSpan.FromSeconds(30));
            cloudProxy.VerifyAll();
        }

        [Fact]
        public async Task ProcessMultipleSubscriptionCallsTest()
        {
            // Arrange
            string id = "d1";

            async Task<Option<ICloudProxy>> DummyProxyGetter()
            {
                await Task.Delay(TimeSpan.FromSeconds(3));
                throw new TimeoutException();
            }

            var cloudProxy = new Mock<ICloudProxy>(MockBehavior.Strict);
            var connectionManager = new Mock<IConnectionManager>(MockBehavior.Strict);
            connectionManager.Setup(c => c.AddSubscription(id, It.IsAny<DeviceSubscription>()));
            connectionManager.Setup(c => c.GetCloudConnection(id))
                .Returns(DummyProxyGetter);
            SubscriptionProcessor subscriptionProcessor = GetSubscriptionProcessor(connectionManager.Object);

            // Act
            await subscriptionProcessor.AddSubscription(id, DeviceSubscription.C2D);
            await subscriptionProcessor.AddSubscription(id, DeviceSubscription.DesiredPropertyUpdates);
            await subscriptionProcessor.AddSubscription(id, DeviceSubscription.Methods);
            await subscriptionProcessor.AddSubscription(id, DeviceSubscription.TwinResponse);

            // Assert
            await Task.Delay(TimeSpan.FromSeconds(5));
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
            await Task.Delay(TimeSpan.FromSeconds(5));
            cloudProxy.VerifyAll();

            cloudProxy = new Mock<ICloudProxy>(MockBehavior.Strict);
            connectionManager = Mock.Of<IConnectionManager>(c => c.GetCloudConnection(id) == Task.FromResult(Option.Some(cloudProxy.Object)));
            subscriptionProcessor = GetSubscriptionProcessor(connectionManager);

            // Act
            await subscriptionProcessor.RemoveSubscription(id, DeviceSubscription.ModuleMessages);
            await subscriptionProcessor.RemoveSubscription(id, DeviceSubscription.TwinResponse);
            await subscriptionProcessor.RemoveSubscription(id, DeviceSubscription.Unknown);

            // Assert
            await Task.Delay(TimeSpan.FromSeconds(5));
            cloudProxy.VerifyAll();
        }

        [Fact]
        public async Task AddSubscriptionHandlesExceptionTest()
        {
            // Arrange
            string deviceId = "d1";
            var cloudProxy = new Mock<ICloudProxy>();
            cloudProxy.Setup(c => c.SetupCallMethodAsync())
                .ThrowsAsync(new InvalidOperationException());

            var connectionManager = new Mock<IConnectionManager>();
            connectionManager.Setup(c => c.AddSubscription(deviceId, DeviceSubscription.Methods));
            connectionManager.Setup(c => c.GetCloudConnection(deviceId)).Returns(Task.FromResult(Option.Some(cloudProxy.Object)));
            var subscriptionProcessor = GetSubscriptionProcessor(connectionManager.Object);

            // Act
            await subscriptionProcessor.AddSubscription(deviceId, DeviceSubscription.Methods);

            // Assert
            await Task.Delay(TimeSpan.FromSeconds(5));
            cloudProxy.VerifyAll();
            connectionManager.VerifyAll();
        }

        [Fact]
        public async Task AddSubscriptionTimesOutTest()
        {
            // Arrange
            string deviceId = "d1";
            var cloudProxy = new Mock<ICloudProxy>();
            cloudProxy.Setup(c => c.SetupCallMethodAsync())
                .ThrowsAsync(new TimeoutException());

            var connectionManager = new Mock<IConnectionManager>();
            connectionManager.Setup(c => c.AddSubscription(deviceId, DeviceSubscription.Methods));
            connectionManager.Setup(c => c.GetCloudConnection(deviceId)).Returns(Task.FromResult(Option.Some(cloudProxy.Object)));
            var subscriptionProcessor = GetSubscriptionProcessor(connectionManager.Object);

            // Act
            await subscriptionProcessor.AddSubscription(deviceId, DeviceSubscription.Methods);

            // Assert
            await Task.Delay(TimeSpan.FromSeconds(5));
            cloudProxy.VerifyAll();
            connectionManager.VerifyAll();
        }

        [Fact]
        public async Task RemoveSubscriptionHandlesExceptionTest()
        {
            // Arrange
            string deviceId = "d1";
            var cloudProxy = new Mock<ICloudProxy>();
            cloudProxy.Setup(c => c.SetupCallMethodAsync())
                .ThrowsAsync(new InvalidOperationException());

            var connectionManager = new Mock<IConnectionManager>();
            connectionManager.Setup(c => c.AddSubscription(deviceId, DeviceSubscription.Methods));
            connectionManager.Setup(c => c.GetCloudConnection(deviceId)).Returns(Task.FromResult(Option.Some(cloudProxy.Object)));
            var subscriptionProcessor = GetSubscriptionProcessor(connectionManager.Object);

            // Act
            await subscriptionProcessor.AddSubscription(deviceId, DeviceSubscription.Methods);

            // Assert
            await Task.Delay(TimeSpan.FromSeconds(5));
            cloudProxy.VerifyAll();
            connectionManager.VerifyAll();
        }

        [Fact]
        public async Task RemoveSubscriptionTimesOutTest()
        {
            // Arrange
            string deviceId = "d1";
            var cloudProxy = new Mock<ICloudProxy>();
            cloudProxy.Setup(c => c.SetupCallMethodAsync())
                .ThrowsAsync(new TimeoutException());

            var connectionManager = new Mock<IConnectionManager>();
            connectionManager.Setup(c => c.AddSubscription(deviceId, DeviceSubscription.Methods));
            connectionManager.Setup(c => c.GetCloudConnection(deviceId)).Returns(Task.FromResult(Option.Some(cloudProxy.Object)));
            var subscriptionProcessor = GetSubscriptionProcessor(connectionManager.Object);

            // Act
            await subscriptionProcessor.AddSubscription(deviceId, DeviceSubscription.Methods);

            // Assert
            await Task.Delay(TimeSpan.FromSeconds(5));
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
    }
}
