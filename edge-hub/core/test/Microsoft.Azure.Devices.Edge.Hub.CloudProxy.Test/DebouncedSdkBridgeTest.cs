// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy.Test
{
    using System;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;

    [Unit]
    public class DebouncedSdkBridgeTest
    {
        [Fact]
        public async Task FlapSettlingConnectedProducesOneFastTransition()
        {
            var manager = CreateConnectivityManager();
            var underlying = CreateUnderlyingClient(out ConnectionStatusChangesHandler sdkHandler);
            using var client = new ConnectivityAwareClient(
                underlying.Object,
                manager.Object,
                Mock.Of<IIdentity>(i => i.Id == "d1/$edgeHub"));

            var connected = new TaskCompletionSource<DateTime>(TaskCreationOptions.RunContinuationsAsynchronously);
            var disconnected = new TaskCompletionSource<DateTime>(TaskCreationOptions.RunContinuationsAsynchronously);
            client.SetConnectionStatusChangedHandler((status, reason) =>
            {
                if (status == ConnectionStatus.Connected)
                {
                    connected.TrySetResult(DateTime.UtcNow);
                }
                else
                {
                    disconnected.TrySetResult(DateTime.UtcNow);
                }
            });

            await client.OpenAsync();

            // Establish a disconnected baseline so the final Connected edge is observable.
            sdkHandler(ConnectionStatus.Disconnected, ConnectionStatusChangeReason.No_Network);
            await disconnected.Task.WaitAsync(TimeSpan.FromSeconds(5));

            manager.Invocations.Clear();
            connected = new TaskCompletionSource<DateTime>(TaskCreationOptions.RunContinuationsAsynchronously);
            disconnected = new TaskCompletionSource<DateTime>(TaskCreationOptions.RunContinuationsAsynchronously);

            for (int i = 0; i < 10; i++)
            {
                sdkHandler(ConnectionStatus.Disconnected, ConnectionStatusChangeReason.No_Network);
                sdkHandler(ConnectionStatus.Connected, ConnectionStatusChangeReason.Connection_Ok);
                await Task.Delay(50);
            }

            DateTime finalEdge = DateTime.UtcNow;
            sdkHandler(ConnectionStatus.Connected, ConnectionStatusChangeReason.Connection_Ok);
            DateTime notification = await connected.Task.WaitAsync(TimeSpan.FromSeconds(5));
            TimeSpan elapsed = notification - finalEdge;

            Assert.InRange(elapsed, TimeSpan.FromSeconds(1.5), TimeSpan.FromSeconds(4));
            Assert.False(disconnected.Task.IsCompleted);
            manager.Verify(m => m.CallSucceeded(), Times.Once);
            manager.Verify(m => m.CallTimedOut(), Times.Never);
        }

        [Fact]
        public async Task SustainedDisconnectProducesOneFastTransition()
        {
            var manager = CreateConnectivityManager();
            var underlying = CreateUnderlyingClient(out ConnectionStatusChangesHandler sdkHandler);
            using var client = new ConnectivityAwareClient(
                underlying.Object,
                manager.Object,
                Mock.Of<IIdentity>(i => i.Id == "d1/$edgeHub"));

            var disconnected = new TaskCompletionSource<DateTime>(TaskCreationOptions.RunContinuationsAsynchronously);
            client.SetConnectionStatusChangedHandler((status, reason) =>
            {
                if (status != ConnectionStatus.Connected)
                {
                    disconnected.TrySetResult(DateTime.UtcNow);
                }
            });

            await client.OpenAsync();
            manager.Invocations.Clear();

            DateTime edge = DateTime.UtcNow;
            sdkHandler(ConnectionStatus.Disconnected, ConnectionStatusChangeReason.No_Network);
            DateTime notification = await disconnected.Task.WaitAsync(TimeSpan.FromSeconds(5));
            TimeSpan elapsed = notification - edge;

            Assert.InRange(elapsed, TimeSpan.FromSeconds(1.5), TimeSpan.FromSeconds(4));
            manager.Verify(m => m.CallTimedOut(), Times.Once);
            manager.Verify(m => m.CallSucceeded(), Times.Never);
        }

        [Fact]
        public async Task CloseCancelsPendingTransition()
        {
            var manager = CreateConnectivityManager();
            var underlying = CreateUnderlyingClient(out ConnectionStatusChangesHandler sdkHandler);
            using var client = new ConnectivityAwareClient(
                underlying.Object,
                manager.Object,
                Mock.Of<IIdentity>(i => i.Id == "d1/$edgeHub"));

            int disconnected = 0;
            client.SetConnectionStatusChangedHandler((status, reason) =>
            {
                if (status != ConnectionStatus.Connected)
                {
                    Interlocked.Increment(ref disconnected);
                }
            });

            await client.OpenAsync();
            manager.Invocations.Clear();

            sdkHandler(ConnectionStatus.Disconnected, ConnectionStatusChangeReason.No_Network);
            await client.CloseAsync();
            await Task.Delay(TimeSpan.FromSeconds(3));

            Assert.Equal(0, disconnected);
            manager.Verify(m => m.CallTimedOut(), Times.Never);
        }

        static Mock<IDeviceConnectivityManager> CreateConnectivityManager()
        {
            var manager = new Mock<IDeviceConnectivityManager>();
            manager.Setup(m => m.CallSucceeded()).Returns(Task.CompletedTask);
            manager.Setup(m => m.CallTimedOut()).Returns(Task.CompletedTask);
            return manager;
        }

        static Mock<IClient> CreateUnderlyingClient(out ConnectionStatusChangesHandler sdkHandler)
        {
            ConnectionStatusChangesHandler capturedHandler = null;
            var underlying = new Mock<IClient>();
            underlying.Setup(c => c.SetConnectionStatusChangedHandler(It.IsAny<ConnectionStatusChangesHandler>()))
                .Callback<ConnectionStatusChangesHandler>(handler => capturedHandler = handler);
            underlying.Setup(c => c.OpenAsync()).Returns(Task.CompletedTask);
            underlying.Setup(c => c.CloseAsync()).Returns(Task.CompletedTask);
            sdkHandler = (status, reason) => capturedHandler(status, reason);
            return underlying;
        }
    }
}
