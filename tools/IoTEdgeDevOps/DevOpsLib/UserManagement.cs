// Copyright (c) Microsoft. All rights reserved.
namespace DevOpsLib
{
    using System;
    using System.Threading.Tasks;
    using DevOpsLib.VstsModels;
    using Flurl;
    using Flurl.Http;
    using Newtonsoft.Json.Linq;

    public class UserManagement
    {
        const string UserPathSegmentFormat = "{0}/{1}/_apis/graph/users";

        readonly DevOpsAccessSetting accessSetting;

        public UserManagement(DevOpsAccessSetting accessSetting)
        {
            this.accessSetting = accessSetting;
        }

        /// <summary>
        /// This method is used to create a bug in Azure Dev Ops.
        /// If it cannot create the bug it will rethrow the exception from the DevOps api.
        /// Reference: https://docs.microsoft.com/en-us/rest/api/azure/devops/wit/work%20items/create?view=azure-devops-rest-6.0
        /// </summary>
        /// <param name="branch">Branch for which the bug is being created</param>
        /// <param name="build">Build for which the bug is being created</param>
        /// <returns>Work item id for the created bug.</returns>
        public async Task ListUsersAsync()
        {
            string requestPath = string.Format(UserPathSegmentFormat, DevOpsAccessSetting.UserManagementBaseUrl, DevOpsAccessSetting.IotedgeOrganization);

            IFlurlRequest workItemQueryRequest = ((Url)requestPath)
                .WithBasicAuth(string.Empty, this.accessSetting.IotedgePAT)
                .WithHeader("Content-Type", "application/json")
                .SetQueryParam("api-version", "6.0-preview.1");


            JObject result;
            try
            {
                IFlurlResponse response = await workItemQueryRequest.GetAsync();

                result = await response.GetJsonAsync<JObject>();

                Console.WriteLine(result.ToString());
                Console.ReadLine();
            }
            catch (FlurlHttpException e)
            {
                string message = $"Failed making call to list user api: {e.Message}";
                Console.ReadLine();
                Console.WriteLine(message);
                Console.WriteLine(e.Call.RequestBody);
                Console.WriteLine(e.Call.Response.StatusCode);
                Console.WriteLine(e.Call.Response.ResponseMessage);

                throw new Exception(message);
            }
        }
    }
}

