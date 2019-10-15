// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.Test.Edgedeployment
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Rest;
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

        [Fact]
        public void EdgeDeploymentStatusSuccessFactoryIsSuccessStatus()
        {
            var status = EdgeDeploymentStatus.Success("deployed");
            Assert.Equal(EdgeDeploymentStatusType.Success, status.State);
            Assert.Equal("deployed", status.Message);
        }

        [Fact]
        public void EdgeDeploymentStatusExceptionFailureFactoryIsFailureStatus()
        {
            Exception e = new Exception("deployment failed");
            var status = EdgeDeploymentStatus.Failure(e);
            Assert.Equal(EdgeDeploymentStatusType.Failure, status.State);
            Assert.Equal("deployment failed", status.Message);

            var uri = new Uri("https://my-uri");
            HttpOperationException httpEx = new HttpOperationException("HTTP Exception")
            {
                Request = new HttpRequestMessageWrapper(new HttpRequestMessage(HttpMethod.Post, uri), string.Empty)
            };
            var httpStatus = EdgeDeploymentStatus.Failure(httpEx);
            Assert.Equal(EdgeDeploymentStatusType.Failure, httpStatus.State);
            Assert.Equal($"POST [{uri}](HTTP Exception)", httpStatus.Message);
        }

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
