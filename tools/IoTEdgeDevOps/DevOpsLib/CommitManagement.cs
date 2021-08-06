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

    // TODO ANDREW: Update description here and elsewhere
    public class CommitManagement
    {
        const string UserPathSegmentFormat = "https://api.github.com/repos/Azure/iotedge/commits/{0}";

        public CommitManagement()
        {
        }

        /// <summary>
        /// This method is used to create a bug in Azure Dev Ops.
        /// If it cannot create the bug it will rethrow the exception from the DevOps api.
        /// Reference: https://docs.microsoft.com/en-us/rest/api/azure/devops/wit/work%20items/create?view=azure-devops-rest-6.0
        /// </summary>
        /// <param name="branch">Branch for which the bug is being created</param>
        /// <param name="build">Build for which the bug is being created</param>
        /// <returns>Work item id for the created bug.</returns>
        public async Task<string> GetAuthorFullNameFromCommitAsync(string commit)
        {
            string requestPath = string.Format(UserPathSegmentFormat, commit);

            IFlurlRequest workItemQueryRequest = ((Url)requestPath)
                .WithHeader("Content-Type", "application/json")
                .WithHeader("User-Agent", "Azure/iotedge");

            JObject result;
            try
            {
                IFlurlResponse response = await workItemQueryRequest.GetAsync();
                result = await response.GetJsonAsync<JObject>();
                string fullName = result["commit"]["author"]["name"].ToString();
                return fullName;
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


