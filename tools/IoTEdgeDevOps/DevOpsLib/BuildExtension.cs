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
                BuildDefinitionId.NestedEndToEndTest,
                BuildDefinitionId.ConnectivityTest,
                BuildDefinitionId.NestedConnectivityTest,
                BuildDefinitionId.LonghaulTestEnv1,
                BuildDefinitionId.LonghaulTestEnv2,
                BuildDefinitionId.LonghaulTestEnv3,
                BuildDefinitionId.NestedLonghaulTest,
                BuildDefinitionId.StressTestEnv1,
                BuildDefinitionId.StressTestEnv2,
                BuildDefinitionId.StressTestEnv3
            };
        static Dictionary<BuildDefinitionId, string> definitionIdToDisplayNameMapping = new Dictionary<BuildDefinitionId, string>
        {
            { BuildDefinitionId.BuildImages, "Build Images" },
            { BuildDefinitionId.CI, "CI" },
            { BuildDefinitionId.EdgeletCI, "Edgelet CI" },
            { BuildDefinitionId.EdgeletPackages, "Edgelet Packages" },
            { BuildDefinitionId.EdgeletRelease, "Edgelet Release" },
            { BuildDefinitionId.EndToEndTest, "New E2E Test" },
            { BuildDefinitionId.NestedEndToEndTest, "Nested E2E Test" },
            { BuildDefinitionId.ImageRelease, "Image Release" },
            { BuildDefinitionId.LibiohsmCI, "Libiothsm CI" },
            { BuildDefinitionId.ConnectivityTest, "Connectivity Test" },
            { BuildDefinitionId.NestedConnectivityTest, "Nested Connectivity Test" },
            { BuildDefinitionId.LonghaulTestEnv1, "Longhaul Test" },
            { BuildDefinitionId.LonghaulTestEnv2, "Longhaul Test Release Candidate" },
            { BuildDefinitionId.LonghaulTestEnv3, "Longhaul Test Release" },
            { BuildDefinitionId.NestedLonghaulTest, "Nested Longhaul Test" },
            { BuildDefinitionId.StressTestEnv1, "Stress Test" },
            { BuildDefinitionId.StressTestEnv2, "Stress Test Release Candidate" },
            { BuildDefinitionId.StressTestEnv3, "Stress Test Release" },
        };

        public static string DisplayName(this BuildDefinitionId buildDefinitionId)
        {
            return definitionIdToDisplayNameMapping.ContainsKey(buildDefinitionId) ? definitionIdToDisplayNameMapping[buildDefinitionId] : buildDefinitionId.ToString();
        }

        public static string IdString(this BuildDefinitionId buildDefinitionId) => ((int)buildDefinitionId).ToString();
    }
}
