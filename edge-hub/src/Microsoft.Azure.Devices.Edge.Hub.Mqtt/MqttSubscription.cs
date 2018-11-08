// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Edge.Hub.Mqtt
{
    using System;

    using DotNetty.Codecs.Mqtt.Packets;

    using Microsoft.Azure.Devices.ProtocolGateway.Mqtt.Persistence;

    using Newtonsoft.Json;

    public class MqttSubscription : ISubscription
    {
        public MqttSubscription(string topicFilter, QualityOfService qualityOfService)
            : this(DateTime.UtcNow, topicFilter, qualityOfService)
        {
        }

        [JsonConstructor]
        public MqttSubscription(DateTime creationTime, string topicFilter, QualityOfService qualityOfService)
        {
            this.CreationTime = creationTime;
            this.TopicFilter = topicFilter;
            this.QualityOfService = qualityOfService;
        }

        public DateTime CreationTime { get; }

        public QualityOfService QualityOfService { get; }

        public string TopicFilter { get; }

        public ISubscription CreateUpdated(QualityOfService qos) => new MqttSubscription(this.CreationTime, this.TopicFilter, qos);
    }
}
