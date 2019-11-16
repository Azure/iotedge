// Copyright (c) Microsoft. All rights reserved.
namespace DevOpsLib
{
    using System.Collections.Generic;

    public static class BuildDefinitionExtension
    {
        public static HashSet<BuildDefinitionId> MasterBranchReporting =>
            new HashSet<BuildDefinitionId>
            {
                BuildDefinitionId.CI,
                BuildDefinitionId.EdgeletCI,
                BuildDefinitionId.LibiohsmCI,
                BuildDefinitionId.BuildImages,
                BuildDefinitionId.EdgeletPackages,
                BuildDefinitionId.EndToEndTest
            };

        public static string DisplayName(this BuildDefinitionId buildDefinitionId)
        {
            Dictionary<BuildDefinitionId, string> buildDefinitionIdToDisplayNameMapping = new Dictionary<BuildDefinitionId, string>
            {
                { BuildDefinitionId.BuildImages, "Build Images" },
                { BuildDefinitionId.CI, "Build Images" },
                { BuildDefinitionId.EdgeletCI, "Edgelet CI" },
                { BuildDefinitionId.EdgeletPackages, "Edgelet Packages" },
                { BuildDefinitionId.EdgeletRelease, "Edgelet Release" },
                { BuildDefinitionId.EndToEndTest, "End-to-End Test" },
                { BuildDefinitionId.ImageRelease, "Image Release" },
                { BuildDefinitionId.LibiohsmCI, "Libiohsm CI" },
            };

            return buildDefinitionIdToDisplayNameMapping.ContainsKey(buildDefinitionId) ? buildDefinitionIdToDisplayNameMapping[buildDefinitionId] : buildDefinitionId.ToString();
        }

        public static string IdString(this BuildDefinitionId buildDefinitionId) => ((int) buildDefinitionId).ToString();
    }

    public enum BuildDefinitionId
    {
        BuildImages = 55174,
        CI = 45137,
        EdgeletCI = 37729,
        EdgeletPackages = 55463,
        EdgeletRelease = 31845,
        EndToEndTest = 87020,
        ImageRelease = 31987,
        LibiohsmCI = 39853
    }
}
