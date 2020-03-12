// Copyright (c) Microsoft. All rights reserved.
namespace DevOpsLib
{
    public class DevOpsAccessSetting
    {
        public const string BaseUrl = "https://dev.azure.com";
        public const string ReleaseManagementBaseUrl = "https://vsrm.dev.azure.com";
        public const string AzureOrganization = "msazure";
        public const string AzureProject = "one";
        public const string IoTTeam = "IoT-Platform-Edge";

        public DevOpsAccessSetting(string personalAccessToken)
            : this(AzureOrganization, AzureProject, personalAccessToken, IoTTeam)
        {
        }

        public DevOpsAccessSetting(
            string organization,
            string project,
            string personalAccessToken,
            string team)
        {
            ValidationUtil.ThrowIfNullOrWhiteSpace(organization, nameof(organization));
            ValidationUtil.ThrowIfNullOrWhiteSpace(project, nameof(project));
            ValidationUtil.ThrowIfNullOrWhiteSpace(personalAccessToken, nameof(personalAccessToken));
            ValidationUtil.ThrowIfNullOrWhiteSpace(team, nameof(team));

            this.Organization = organization;
            this.Project = project;
            this.PersonalAccessToken = personalAccessToken;
            this.Team = team;
        }

        public string Organization { get; }

        public string Project { get; }

        public string PersonalAccessToken { get; }

        public string Team { get; }
    }
}
