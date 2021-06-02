// Copyright (c) Microsoft. All rights reserved.
namespace IotEdgeQuickstart.Details
{
    internal class UriSocks
    {
        public UriSocks(string ConnectManagementUri, string ConnectWorkloadUri, string ListenManagementUri, string ListenWorkloadUri)
        {
            this.ConnectManagement = string.IsNullOrEmpty(ConnectManagementUri) ? "unix:///var/run/iotedge/mgmt.sock" : ConnectManagementUri;
            this.ConnectWorkload = string.IsNullOrEmpty(ConnectWorkloadUri) ? "unix:///var/run/iotedge/workload.sock" : ConnectWorkloadUri;
            this.ListenManagement = string.IsNullOrEmpty(ListenManagementUri) ? "fd://aziot-edged.mgmt.socket" : ListenManagementUri;
            this.ListenWorkload = string.IsNullOrEmpty(ListenWorkloadUri) ? "fd://aziot-edged.workload.socket" : ListenWorkloadUri;
        }

        public string ConnectManagement { get; }

        public string ConnectWorkload { get; }

        public string ListenManagement { get; }

        public string ListenWorkload { get; }
    }
}
