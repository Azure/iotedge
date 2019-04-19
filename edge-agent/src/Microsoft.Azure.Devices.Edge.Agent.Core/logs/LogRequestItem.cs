// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Logs
{
    using Microsoft.Azure.Devices.Edge.Util;
    using Newtonsoft.Json;

    public class LogRequestItem
    {
        public LogRequestItem(string id, ModuleLogFilter filter)
        {
            this.Id = Preconditions.CheckNonWhiteSpace(id, nameof(id));
            this.Filter = filter ?? ModuleLogFilter.Empty;
        }

        [JsonProperty("id")]
        public string Id { get; }

        [JsonProperty("filter")]
        public ModuleLogFilter Filter { get; }
    }
}
