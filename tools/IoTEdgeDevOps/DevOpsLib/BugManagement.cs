// Copyright (c) Microsoft. All rights reserved.
namespace DevOpsLib
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using DevOpsLib.VstsModels;
    using Flurl;
    using Flurl.Http;
    using Newtonsoft.Json.Linq;

    public class BugManagement
    {
        const string WorkItemPathSegmentFormat = "{0}/{1}/{2}/_apis/wit/workitems/$Bug";
        const string BackupOnCallReportLink = "https://microsoft.sharepoint.com/teams/Azure_IoT/_layouts/15/Doc.aspx?sourcedoc={1d485f94-0812-41c5-81b1-7fc0ca2dc9e4}&action=edit&wd=target%28Azure%20IoT%20Edge%2FServicing.one%7C0E88E43B-8A10-4A3F-982E-CF8B7441EAE8%2FBackup%20On-Call%20Reports%7Cfda0177f-3b35-4751-bfc9-7420cd56652c%2F%29&wdorigin=703";

        readonly DevOpsAccessSetting accessSetting;
        readonly CommitManagement commitManagement;
        readonly UserManagement userManagement;

        public BugManagement(DevOpsAccessSetting accessSetting, CommitManagement commitManagement, UserManagement userManagement)
        {
            this.accessSetting = accessSetting;
            this.commitManagement = commitManagement;
            this.userManagement = userManagement;
        }

        /// <summary>
        /// This method is used to create a bug in Azure Dev Ops.
        /// If it cannot create the bug it will rethrow the exception from the DevOps api.
        /// Reference: https://docs.microsoft.com/en-us/rest/api/azure/devops/wit/work%20items/create?view=azure-devops-rest-6.0
        /// </summary>
        /// <param name="branch">Branch for which the bug is being created</param>
        /// <param name="build">Build for which the bug is being created</param>
        /// <returns>Work item id for the created bug.</returns>
        public async Task<string> CreateBugAsync(string branch, VstsBuild build)
        {
            string requestPath = string.Format(WorkItemPathSegmentFormat, DevOpsAccessSetting.BaseUrl, DevOpsAccessSetting.AzureOrganization, DevOpsAccessSetting.AzureProject);
            IFlurlRequest workItemQueryRequest = ((Url)requestPath)
                .WithBasicAuth(string.Empty, this.accessSetting.MsazurePAT)
                .WithHeader("Content-Type", "application/json-patch+json")
                .SetQueryParam("api-version", "6.0");

            (string bugOwnerFullName, string bugOwnerEmail) = await this.GetBugOwnerInfoAsync(build.SourceVersion);
            string bugDescription = GenerateBugDescription(bugOwnerFullName, bugOwnerEmail, build);

            var jsonBody = new object[]
            {
                new
                {
                    op = "add",
                    path = "/fields/System.Title",
                    from = string.Empty,
                    value = $"Test failure on {branch}: {build.DefinitionId.ToString()} {build.BuildNumber}"
                },
                new
                {
                    op = "add",
                    path = "/fields/Microsoft.VSTS.TCM.ReproSteps",
                    from = string.Empty,
                    value = bugDescription
                },
                new
                {
                    op = "add",
                    path = "/fields/Microsoft.VSTS.Common.Priority",
                    from = string.Empty,
                    value = "0"
                },
                new
                {
                    op = "add",
                    path = "/fields/System.AreaPath",
                    from = string.Empty,
                    value = "One\\IoT\\Platform and Devices\\IoTEdge"
                },
                new
                {
                    op = "add",
                    path = "/relations/-",
                    value = new
                    {
                        rel = "Hyperlink",
                        url = $"{build.WebUri}"
                    }
                },
                new
                {
                    op = "add",
                    path = "/fields/System.AssignedTo",
                    value = bugOwnerEmail
                },
                new
                {
                    op = "add",
                    path = "/fields/System.Tags",
                    value = "auto-pipeline-failed"
                }
            };

            JObject result;
            try
            {
                IFlurlResponse response = await workItemQueryRequest
                    .PostJsonAsync(jsonBody);

                result = await response.GetJsonAsync<JObject>();
            }
            catch (FlurlHttpException e)
            {
                string message = $"Failed making call to vsts work item api: {e.Message}";
                Console.WriteLine(message);
                Console.WriteLine(e.Call.RequestBody);
                Console.WriteLine(e.Call.Response.StatusCode);
                Console.WriteLine(e.Call.Response.ResponseMessage);

                throw new Exception(message);
            }

            return result["id"].ToString();
        }

        static string GenerateBugDescription(string bugOwnerFullName, string bugOwnerEmail, VstsBuild build)
        {
            string bugDescription = "This bug is autogenerated and assigned by the vsts-pipeline-sync service. ";
            if (bugOwnerEmail.Equals(string.Empty))
            {
                bugDescription = $"Attempted to assign to {bugOwnerFullName}, but failed to do so. Either this person is not a team member or they are not in the iotedge devops organization.";
            }
            else
            {
                bugDescription = $"Assigned to {bugOwnerFullName}.";
            }

            bugDescription += $"<div>`<div> Please address if the failure was caused by your changes. Otherwise please help to triage appropriately. ";
            bugDescription += $"Reference the backup on-call report to match to an existing bug. If the bug does not exist yet, please create a new bug and coordinate with backup on-call to confirm the bug gets added to the most recent backup on-call report. After this is complete you can close this bug. Link to resource: <div> <a href=\"{BackupOnCallReportLink}\">Backup On-Call Reports</a> <div>`<div>";
            bugDescription += $"Link to failing build:<div> <a href=\"{build.WebUri}\">Failing Build</a>";

            return bugDescription;
        }

        async Task<(string, string)> GetBugOwnerInfoAsync(string commit)
        {
            string ownerFullName = await this.commitManagement.GetAuthorFullNameFromCommitAsync(commit);

            IList<VstsUser> allTeamMembers = await this.userManagement.ListUsersAsync();
            foreach (VstsUser user in allTeamMembers)
            {
                if (user.Name == ownerFullName)
                {
                    return (ownerFullName, user.MailAddress);
                }
            }

            return (ownerFullName, String.Empty);
        }
    }
}
