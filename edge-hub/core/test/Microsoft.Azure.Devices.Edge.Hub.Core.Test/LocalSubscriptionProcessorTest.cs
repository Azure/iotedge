// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Test
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Amqp.Serialization;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;

    [Unit]
    public class LocalSubscriptionProcessorTest
    {
        [Theory]
        [MemberData(nameof(SmokeTestData))]
        public async Task SmokeTest(List<(DeviceSubscription, bool)> subscriptions, bool batch)
        {
            // Arrange
            string id = "d1";
            var addedSubscriptions = new List<DeviceSubscription>();
            var removedSubscriptions = new List<DeviceSubscription>();
            var connectionManager = new Mock<IConnectionManager>();
            connectionManager.Setup(c => c.AddSubscription(id, It.IsAny<DeviceSubscription>()))
                .Callback<string, DeviceSubscription>((_, s) => addedSubscriptions.Add(s));
            connectionManager.Setup(c => c.RemoveSubscription(id, It.IsAny<DeviceSubscription>()))
                .Callback<string, DeviceSubscription>((_, s) => removedSubscriptions.Add(s));
            var localSubscriptionProcessor = new LocalSubscriptionProcessor(connectionManager.Object);

            // Act
            if (batch)
            {
                await localSubscriptionProcessor.ProcessSubscriptions(id, subscriptions);
            }
            else
            {
                foreach ((DeviceSubscription subscription, bool enable) sub in subscriptions)
                {
                    if (sub.enable)
                    {
                        await localSubscriptionProcessor.AddSubscription(id, sub.subscription);
                    }
                    else
                    {
                        await localSubscriptionProcessor.RemoveSubscription(id, sub.subscription);
                    }
                }
            }

            // Assert
            foreach ((DeviceSubscription subscription, bool enable) sub in subscriptions)
            {
                if (sub.enable)
                {
                    Assert.Contains(sub.subscription, addedSubscriptions);
                    Assert.DoesNotContain(sub.subscription, removedSubscriptions);
                }
                else
                {
                    Assert.DoesNotContain(sub.subscription, addedSubscriptions);
                    Assert.Contains(sub.subscription, removedSubscriptions);
                }
            }
        }

        public static IEnumerable<object[]> SmokeTestData()
        {
            var subscriptions1 = new List<(DeviceSubscription subscription, bool enable)>
            {
                (DeviceSubscription.C2D, true),
                (DeviceSubscription.DesiredPropertyUpdates, true),
                (DeviceSubscription.Methods, false),
                (DeviceSubscription.ModuleMessages, false),
                (DeviceSubscription.TwinResponse, true)
            };

            var subscriptions2 = new List<(DeviceSubscription subscription, bool enable)>
            {
                (DeviceSubscription.C2D, false),
                (DeviceSubscription.DesiredPropertyUpdates, false),
                (DeviceSubscription.Methods, true),
                (DeviceSubscription.ModuleMessages, true),
                (DeviceSubscription.TwinResponse, false)
            };

            var subscriptions3 = new List<(DeviceSubscription subscription, bool enable)>
            {
                (DeviceSubscription.DesiredPropertyUpdates, true),
                (DeviceSubscription.ModuleMessages, true)
            };

            var subscriptions4 = new List<(DeviceSubscription subscription, bool enable)>
            {
                (DeviceSubscription.DesiredPropertyUpdates, false),
                (DeviceSubscription.ModuleMessages, false)
            };

            yield return new object[] { subscriptions1, true };
            yield return new object[] { subscriptions2, true };
            yield return new object[] { subscriptions3, true };
            yield return new object[] { subscriptions4, true };
            yield return new object[] { subscriptions1, false };
            yield return new object[] { subscriptions2, false };
            yield return new object[] { subscriptions3, false };
            yield return new object[] { subscriptions4, false };
        }
    }
}
