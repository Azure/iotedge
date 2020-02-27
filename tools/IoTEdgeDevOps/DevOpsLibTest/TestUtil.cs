namespace DevOpsLibTest
{
    using System;
    using System.Collections.Generic;
    using DevOpsLib;
    using DevOpsLib.VstsModels;

    internal static class TestUtil
    {
        public static HashSet<IoTEdgeReleaseDeployment> GetDeployments(int numberOfDeployments)
        {
            return GetDeployments(numberOfDeployments, DateTime.UtcNow);
        }

        public static HashSet<IoTEdgeReleaseDeployment> GetDeployments(int numberOfDeployments, DateTime deploymentStartTime)
        {
            var deployments = new HashSet<IoTEdgeReleaseDeployment>();

            for (int i = 1; i <= numberOfDeployments; i++)
            {
                deployments.Add(
                    new IoTEdgeReleaseDeployment(
                        i,
                        1,
                        VstsDeploymentStatus.InProgress,
                        deploymentStartTime,
                        new HashSet<IoTEdgePipelineTask> {
                            new IoTEdgePipelineTask(
                                1000 + i,
                                $"Task {1000 + i}",
                                "In progress",
                                deploymentStartTime,
                                DateTime.MinValue,
                                new Uri($"https://dummy.com/log/{i}")) }));
            }

            return deployments;
        }

    }
}
