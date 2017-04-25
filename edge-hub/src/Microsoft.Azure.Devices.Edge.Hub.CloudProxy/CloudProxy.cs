// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Cloud;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    class CloudProxy : ICloudProxy
    {
        readonly DeviceClient deviceClient;
        readonly IMessageConverter<Message> messageConverter;
        readonly ILogger logger;

        public CloudProxy(DeviceClient deviceClient, IMessageConverter<Message> messageConverter, ILogger logger)
        {
            this.deviceClient = Preconditions.CheckNotNull(deviceClient, nameof(deviceClient));
            this.messageConverter = Preconditions.CheckNotNull(messageConverter, nameof(messageConverter));
            this.logger = logger;
        }

        public async Task<bool> Disconnect()
        {
            try
            {
                await this.deviceClient.CloseAsync();
                return true;
            }
            catch (Exception ex)
            {
                this.logger.LogError($"Error closing IoTHub connection - {ex.ToString()}");
                return false;
            }            
        }

        public Task<Twin> GetTwin()
        {
            throw new NotImplementedException();
        }

        public async Task<bool> SendMessage(IMessage inputMessage)
        {
            Preconditions.CheckNotNull(inputMessage, nameof(inputMessage));
            Message message = this.messageConverter.FromMessage(inputMessage);
            try
            {
                await this.deviceClient.SendEventAsync(message);
                return true;
            }
            catch (Exception ex)
            {
                this.logger.LogError($"Error sending message to IoTHub - {ex.ToString()}");
                return false;
            }            
        }

        public async Task<bool> SendMessageBatch(IEnumerable<IMessage> inputMessages)
        {
            IEnumerable<Message> messages = Preconditions.CheckNotNull(inputMessages, nameof(inputMessages))
                .Select(inputMessage => this.messageConverter.FromMessage(inputMessage));
            try
            {
                await this.deviceClient.SendEventBatchAsync(messages);
                return true;
            }
            catch (Exception ex)
            {
                this.logger.LogError($"Error sending message batch to IoTHub - {ex.ToString()}");
                return false;
            }
        }

        public Task UpdateReportedProperties(TwinCollection reportedProperties)
        {
            throw new NotImplementedException();
        }
    }
}
