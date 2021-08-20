// Copyright (c) Microsoft. All rights reserved.
namespace DevOpsLib
{
    using System;
    using System.Threading.Tasks;
    using Flurl;
    using Flurl.Http;
    using Newtonsoft.Json.Linq;

    public class CommitManagement
    {
        const string UserPathSegmentFormat = "https://api.github.com/repos/Azure/iotedge/commits/{0}";

        public CommitManagement()
        {
        }

        /// <summary>
        /// This method is used to get a commit author's full name via the github rest api.
        /// Reference: https://docs.github.com/en/rest
        /// For an example response: https://api.github.com/repos/Azure/iotedge/commits/704250b
        /// </summary>
        /// <param name="commit">Commit for which to get the author's full name.</param>
        /// <returns>Full name of author.</returns>
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
                string message = $"Failed making call to commit api: {e.Message}";
                Console.WriteLine(message);
                Console.WriteLine(e.Call.RequestBody);
                Console.WriteLine(e.Call.Response.StatusCode);
                Console.WriteLine(e.Call.Response.ResponseMessage);

                throw new Exception(message);
            }
        }
    }
}
