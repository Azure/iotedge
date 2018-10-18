// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Config
{
    using System;

    using Newtonsoft.Json;

    public class StoreAndForwardConfiguration
    {
        [JsonConstructor]
        public StoreAndForwardConfiguration(int timeToLiveSecs)
        {
            this.TimeToLiveSecs = timeToLiveSecs;
            this.TimeToLive = timeToLiveSecs < 0 ? TimeSpan.MaxValue : TimeSpan.FromSeconds(timeToLiveSecs);
        }

        [JsonIgnore]
        public TimeSpan TimeToLive { get; }

        [JsonProperty(PropertyName = "timeToLiveSecs")]
        public int TimeToLiveSecs { get; }
    }
}
