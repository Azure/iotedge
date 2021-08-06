// Copyright (c) Microsoft. All rights reserved.
namespace DevOpsLib
{
    public class DevOpsAccessSetting
    {
        public const string BaseUrl = "https://dev.azure.com";
        public const string ReleaseManagementBaseUrl = "https://vsrm.dev.azure.com";
        public const string UserManagementBaseUrl = "https://vssps.dev.azure.com";
        public const string AzureOrganization = "msazure";
        public const string AzureProject = "one";
        public const string IoTTeam = "IoT-Platform-Edge";
        public const string IotedgeOrganization = "iotedge";

        public DevOpsAccessSetting(string msazurePAT, string iotedgePAT = "")
        {
            this.MsazurePAT = msazurePAT;
            this.IotedgePAT = iotedgePAT;
        }

        public string MsazurePAT { get; }

        public string IotedgePAT { get; }
    }
}
