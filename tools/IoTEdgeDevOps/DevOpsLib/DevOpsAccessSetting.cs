// Copyright (c) Microsoft. All rights reserved.
namespace DevOpsLib
{
    public class DevOpsAccessSetting
    {
        public const string BaseUrl = "https://dev.azure.com";
        public const string ReleaseManagementBaseUrl = "https://vsrm.dev.azure.com";
        public const string AzureOrganization = "msazure";
        public const string AzureProject = "one";

        public DevOpsAccessSetting(
            string personalAccessToken)
        : this(AzureOrganization, AzureProject, personalAccessToken)
        {
        }

        public DevOpsAccessSetting(
            string organization,
            string project,
            string personalAccessToken)
        {
            this.Organization = organization;
            this.Project = project;
            this.PersonalAccessToken = personalAccessToken;
        }

        public string Organization { get; }

        public string Project { get; }

        public string PersonalAccessToken { get; }
    }
}
