// Copyright (c) Microsoft. All rights reserved.
namespace ModuleUtil.NetworkControllerResult
{
    using Newtonsoft.Json;
    public class NetworkControllerResult
    {
        public string TrackingId { get; set; }

        public string Operation { get; set; }

        public string OperationStatus { get; set; }

        public NetworkStatus NetworkStatus { get; set; }

        public bool Enabled { get; set; }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }
    }
}
