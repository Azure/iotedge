// Copyright (c) Microsoft. All rights reserved.
namespace DevOpsLib
{
    using System.Collections.Generic;

    public static class ReleaseExtension
    {
        public static string DisplayName(this ReleaseDefinitionId releaseDefinitionId)
        {
            var definitionIdToDisplayNameMapping = new Dictionary<ReleaseDefinitionId, string>
            {
                { ReleaseDefinitionId.E2ETest, "Old E2E Test" },
            };

            return definitionIdToDisplayNameMapping.ContainsKey(releaseDefinitionId) ? definitionIdToDisplayNameMapping[releaseDefinitionId] : releaseDefinitionId.ToString();
        }

        public static string IdString(this ReleaseDefinitionId buildDefinitionId) => ((int)buildDefinitionId).ToString();
    }
}
