// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.ModuleUtil
{
    using Microsoft.Azure.Devices.Shared;
    using Newtonsoft.Json;

    public class TwinTestResult
    {
        public string Operation { get; set; }

        public TwinCollection Properties { get; set; }

        public string ErrorMessage { get; set; }

        public string TrackingId { get; set; }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }
    }
}
