// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.Test.Edgedeployment
{
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    [Unit]
    public class EdgeDeploymentStatusTest
    {
        static readonly EdgeDeploymentStatus Status1 = new EdgeDeploymentStatus(EdgeDeploymentStatusType.Failure, "message");
        static readonly EdgeDeploymentStatus Status2 = new EdgeDeploymentStatus(EdgeDeploymentStatusType.Success, "message");
        static readonly EdgeDeploymentStatus Status3 = new EdgeDeploymentStatus(EdgeDeploymentStatusType.Failure, "message1");
        static readonly EdgeDeploymentStatus Status4 = new EdgeDeploymentStatus(EdgeDeploymentStatusType.Failure, "message2");
        static readonly EdgeDeploymentStatus Status5 = new EdgeDeploymentStatus(EdgeDeploymentStatusType.Success, null);
        static readonly EdgeDeploymentStatus Status6 = new EdgeDeploymentStatus(EdgeDeploymentStatusType.Success, null);
        static readonly EdgeDeploymentStatus Status7 = new EdgeDeploymentStatus(EdgeDeploymentStatusType.Failure, "message");
        static readonly EdgeDeploymentStatus Status8 = new EdgeDeploymentStatus(EdgeDeploymentStatusType.Failure, "message");

        public static IEnumerable<object[]> GetDifferentStatus() => new List<object[]>
        {
            new object[] { Status1, Status2 },
            new object[] { Status3, Status4 },
        };

        [Theory]
        [MemberData(nameof(GetDifferentStatus))]
        public void EdgeDeploymentStatusAreDifferentWithDifferentParametersTest(EdgeDeploymentStatus x, EdgeDeploymentStatus y)
        {
            Assert.NotEqual(x, y);
        }

        public static IEnumerable<object[]> GetSameStatus() => new List<object[]>
        {
            new object[] { Status5, Status6 },
            new object[] { Status7, Status8 },
        };

        [Theory]
        [MemberData(nameof(GetSameStatus))]
        public void EdgeDeploymentStatusAreSameWithSameParametersTest(EdgeDeploymentStatus x, EdgeDeploymentStatus y)
        {
            Assert.Equal(x, y);
        }

        [Fact]
        public void EdgeDeploymentStatusReferenceChecks()
        {
            Assert.Equal(Status1, Status1);
            Assert.False(Status2.Equals(null));
        }
    }
}
