// Copyright (c) Microsoft. All rights reserved.
namespace DevOpsLib
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using DevOpsLib.VstsModels;
    using Flurl;
    using Flurl.Http;
    using Newtonsoft.Json;
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
        /// This method is used to retrieve all the team members for the larger iotedge team.
        /// Reference: https://docs.microsoft.com/en-us/rest/api/azure/devops/graph/users/list?view=azure-devops-rest-6.0
        /// </summary>
        /// <returns>List of users. Each containing full name and email.</returns>
        public async Task<IList<VstsUser>> ListUsersAsync()
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
            }
            catch (FlurlHttpException e)
            {
                string message = $"Failed making call to list user api: {e.Message}";
                Console.WriteLine(message);
                Console.WriteLine(e.Call.RequestBody);
                Console.WriteLine(e.Call.Response.StatusCode);
                Console.WriteLine(e.Call.Response.ResponseMessage);

                throw new Exception(message);
            }

            if (!result.ContainsKey("count") || (int)result["count"] <= 0)
            {
                return new VstsUser[0];
            }

            IList<VstsUser> users = JsonConvert.DeserializeObject<VstsUser[]>(result["value"].ToString());
            return users;
        }
    }
}
