// Copyright (c) Microsoft. All rights reserved.
namespace DevOpsLib
{
    using System.Collections.Generic;

    public static class BuildExtension
    {
        public static HashSet<BuildDefinitionId> BuildDefinitions =>
            new HashSet<BuildDefinitionId>
            {
                BuildDefinitionId.CI,
                BuildDefinitionId.EdgeletCI,
                BuildDefinitionId.LibiohsmCI,
                BuildDefinitionId.BuildImages,
                BuildDefinitionId.EdgeletPackages,
                BuildDefinitionId.EndToEndTest,
                BuildDefinitionId.ConnectivityTest,
                BuildDefinitionId.LonghaulTestEnv1,
                BuildDefinitionId.LonghaulTestEnv2,
                BuildDefinitionId.LonghaulTestEnv3,
                BuildDefinitionId.StressTestEnv1,
                BuildDefinitionId.StressTestEnv2,
                BuildDefinitionId.StressTestEnv3
            };

        public static string DisplayName(this BuildDefinitionId buildDefinitionId)
        {
            var definitionIdToDisplayNameMapping = new Dictionary<BuildDefinitionId, string>
            {
                { BuildDefinitionId.BuildImages, "Build Images" },
                { BuildDefinitionId.CI, "CI" },
                { BuildDefinitionId.EdgeletCI, "Edgelet CI" },
                { BuildDefinitionId.EdgeletPackages, "Edgelet Packages" },
                { BuildDefinitionId.EdgeletRelease, "Edgelet Release" },
                { BuildDefinitionId.EndToEndTest, "End-to-End Test" },
                { BuildDefinitionId.ImageRelease, "Image Release" },
                { BuildDefinitionId.LibiohsmCI, "Libiohsm CI" },
                { BuildDefinitionId.ConnectivityTest, "Connectivity Test" },
                { BuildDefinitionId.LonghaulTestEnv1, "Longhaul Test Env 1" },
                { BuildDefinitionId.LonghaulTestEnv2, "Longhaul Test Env 2" },
                { BuildDefinitionId.LonghaulTestEnv3, "Longhaul Test Env 3" },
                { BuildDefinitionId.StressTestEnv1, "Stress Test Env 1" },
                { BuildDefinitionId.StressTestEnv2, "Stress Test Env 2" },
                { BuildDefinitionId.StressTestEnv3, "Stress Test Env 3" },
            };

            return definitionIdToDisplayNameMapping.ContainsKey(buildDefinitionId) ? definitionIdToDisplayNameMapping[buildDefinitionId] : buildDefinitionId.ToString();
        }

        public static string IdString(this BuildDefinitionId buildDefinitionId) => ((int) buildDefinitionId).ToString();
    }
}
