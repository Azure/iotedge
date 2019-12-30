// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.ModuleUtil.NetworkControllerResult
{
    using Newtonsoft.Json;

    public class NetworkControllerResult
    {
        public string TrackingId { get; set; }

        public string Operation { get; set; }

        public string OperationStatus { get; set; }

        public NetworkControllerType NetworkControllerType { get; set; }

        public NetworkControllerStatus NetworkControllerStatus { get; set; }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }
    }
}
