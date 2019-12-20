// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.ModuleUtil
{
    using System;
    using Microsoft.Azure.Devices.Edge.Util;
    using Newtonsoft.Json;

    public class TestOperationResult
    {
        public TestOperationResult(
            string source,
            string type,
            string result,
            DateTime createdAt)
        {
            this.Source = Preconditions.CheckNonWhiteSpace(source, nameof(source));
            this.Type = Preconditions.CheckNonWhiteSpace(type, nameof(type));
            this.Result = Preconditions.CheckNonWhiteSpace(result, nameof(result));
            this.CreatedAt = createdAt;
        }

        public string Source { get; }

        public string Type { get; }

        public string Result { get; }

        public DateTime CreatedAt { get; }

        [JsonIgnore]
        public bool NetworkOn { get; set; }

        [JsonIgnore]
        public DateTime NetworkLastUpdatedTime { get; set; }
    }
}
