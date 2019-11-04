// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Service.Test.ScenarioTests
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Device;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;

    public class AllGoodDeviceProxy : IDeviceProxy, IListenerBoundProxy
    {
        private Random random = new Random(8934759);
        private IDeviceListener deviceListener;

        private string iotHubName = TestContext.IotHubName;
        private string deviceId = TestContext.DeviceId;

        private Action<IMessage> desiredPropertyUpdateAction = _ => { };

        public AllGoodDeviceProxy() => this.Identity = new DeviceIdentity(this.iotHubName, this.deviceId);

        public virtual bool IsActive => true;
        public virtual IIdentity Identity { get; set; }
        public virtual Task CloseAsync(Exception ex) => Task.CompletedTask;
        public virtual Task<Option<IClientCredentials>> GetUpdatedIdentity() => Task.FromResult(Option.Some(new TokenCredentials(this.Identity, "test-token", "test-product-info", true) as IClientCredentials));

        public virtual Task<DirectMethodResponse> InvokeMethodAsync(DirectMethodRequest request)
        {
            this.SendMessageResponseDelayed(request);
            return Task.FromResult(default(DirectMethodResponse)); // response is communicated via the callback in SendMessageResponseDelayed()
        }

        public virtual Task OnDesiredPropertyUpdates(IMessage desiredProperties)
        {
            this.desiredPropertyUpdateAction(desiredProperties);
            return Task.CompletedTask;
        }

        public virtual Task SendC2DMessageAsync(IMessage message)
        {
            this.SendMessageFeedbackDelayed(message);
            return Task.CompletedTask;
        }

        public virtual Task SendMessageAsync(IMessage message, string input)
        {
            this.SendMessageFeedbackDelayed(message);
            return Task.CompletedTask;
        }

        public virtual Task SendTwinUpdate(IMessage twin) => Task.CompletedTask;
        public virtual void SetInactive()
        {
        }

        public AllGoodDeviceProxy WithIdentity(IIdentity identity)
        {
            this.Identity = identity;
            return this;
        }

        public AllGoodDeviceProxy WithHubName(string iotHubName)
        {
            this.iotHubName = iotHubName;
            return this;
        }

        public AllGoodDeviceProxy WithDesiredPropertyUpdateAction(Action<IMessage> desiredPropertyUpdateAction)
        {
            this.desiredPropertyUpdateAction = desiredPropertyUpdateAction;
            return this;
        }

        public void BindListener(IDeviceListener deviceListener)
        {
            this.deviceListener = deviceListener;
        }

        private async void SendMessageFeedbackDelayed(IMessage message)
        {
            if (this.deviceListener == null)
            {
                return;
            }

            if (message.SystemProperties.TryGetValue(SystemProperties.LockToken, out string lockToken))
            {
                var delayMs = 0;
                lock (this.random)
                {
                    delayMs = this.random.Next(50, 200);
                }

                await Task.Delay(delayMs);
                await this.deviceListener.ProcessMessageFeedbackAsync(lockToken, FeedbackStatus.Complete);
            }
        }

        private async void SendMessageResponseDelayed(DirectMethodRequest request)
        {
            if (this.deviceListener == null)
            {
                return;
            }

            var responseMessage = new EdgeMessage(
                                            new byte[] { 0x22, 0x33 },
                                            new Dictionary<string, string>()
                                            {
                                                [SystemProperties.CorrelationId] = request.CorrelationId
                                            },
                                            new Dictionary<string, string>()
                                            {
                                                [SystemProperties.StatusCode] = "200"
                                            });

            var delayMs = 0;
            lock (this.random)
            {
                delayMs = this.random.Next(300, 2000);
            }

            await Task.Delay(delayMs);
            await this.deviceListener.ProcessMethodResponseAsync(responseMessage);
        }
    }
}
