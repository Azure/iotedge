// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using Microsoft.Azure.Devices.Shared;

    using Newtonsoft.Json;

    public class TwinInfo
    {
        [JsonConstructor]
        public TwinInfo(Twin twin, TwinCollection reportedPropertiesPatch)
        {
            this.Twin = twin;
            this.ReportedPropertiesPatch = reportedPropertiesPatch;
        }

        public TwinCollection ReportedPropertiesPatch { get; }

        public Twin Twin { get; }
    }
}
