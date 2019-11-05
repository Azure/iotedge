// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.Test.Edgedeployment
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using k8s;
    using k8s.Models;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Kubernetes.EdgeDeployment;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Rest;
    using Moq;
    using Newtonsoft.Json.Linq;
    using Xunit;
    using Constants = Microsoft.Azure.Devices.Edge.Agent.Kubernetes.Constants;

    [Unit]
    public class EdgeDeploymentOperatorTest
    {
        const string DeviceNamespace = "a-namespace";
        const string ExceptionMessage = "ExceptionMessage";
        static readonly ResourceName ResourceName = new ResourceName("hostname", "deviceId");

        [Fact]
        public async void WhatDeployModulesReturnsIsWhatIsReported()
        {
            var returnedStatus = EdgeDeploymentStatus.Success("Successfully deployed");
            Option<EdgeDeploymentStatus> reportedStatus = Option.None<EdgeDeploymentStatus>();
            var edgeDefinition = new EdgeDeploymentDefinition("v1", "EdgeDeployment", new V1ObjectMeta(name: ResourceName), new List<KubernetesModule>(), null);
            var response = new HttpOperationResponse<object>()
            {
                Body = edgeDefinition,
                Response = new HttpResponseMessage(HttpStatusCode.OK)
            };

            // DeployModules returns a status, confirm this is what is reported.
            var controller = Mock.Of<IEdgeDeploymentController>();
            Mock.Get(controller).Setup(c => c.DeployModulesAsync(It.IsAny<ModuleSet>(), It.IsAny<ModuleSet>()))
                .ReturnsAsync(returnedStatus);

            var client = Mock.Of<IKubernetes>();
            Mock.Get(client).Setup(c => c.ReplaceNamespacedCustomObjectStatusWithHttpMessagesAsync(It.IsAny<object>(), Constants.EdgeDeployment.Group, Constants.EdgeDeployment.Version, DeviceNamespace, Constants.EdgeDeployment.Plural, It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
                .Callback((object o, string group, string version, string _namespace, string plural, string name, Dictionary<string, List<string>> headers, CancellationToken token) =>
                {
                    Assert.True(o is JObject);
                    EdgeDeploymentDefinition e = ((JObject)o).ToObject<EdgeDeploymentDefinition>();
                    reportedStatus = e.Status;
                })
                .ReturnsAsync(response);

            var edgeOperator = new EdgeDeploymentOperator(
                ResourceName,
                DeviceNamespace,
                client,
                controller);

            await edgeOperator.EdgeDeploymentOnEventHandlerAsync(WatchEventType.Added, edgeDefinition);

            Assert.True(reportedStatus.HasValue);
            Assert.Equal(returnedStatus, reportedStatus.OrDefault());

            Mock.Get(controller).VerifyAll();
            Mock.Get(client).VerifyAll();
        }

        [Fact]
        public async void GettingSameStatusTwiceReportsOnce()
        {
            var returnedStatus = EdgeDeploymentStatus.Success("Successfully deployed");
            var edgeDefinition = new EdgeDeploymentDefinition("v1", "EdgeDeployment", new V1ObjectMeta(name: ResourceName), new List<KubernetesModule>(), null);
            var response = new HttpOperationResponse<object>()
            {
                Body = edgeDefinition,
                Response = new HttpResponseMessage(HttpStatusCode.OK)
            };

            // DeployModules returns a status, confirm this is what is reported.
            var controller = Mock.Of<IEdgeDeploymentController>();
            Mock.Get(controller).Setup(c => c.DeployModulesAsync(It.IsAny<ModuleSet>(), It.IsAny<ModuleSet>()))
                .ReturnsAsync(returnedStatus);

            var client = Mock.Of<IKubernetes>();
            Mock.Get(client).Setup(c => c.ReplaceNamespacedCustomObjectStatusWithHttpMessagesAsync(It.IsAny<object>(), Constants.EdgeDeployment.Group, Constants.EdgeDeployment.Version, DeviceNamespace, Constants.EdgeDeployment.Plural, It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(response);

            var edgeOperator = new EdgeDeploymentOperator(
                ResourceName,
                DeviceNamespace,
                client,
                controller);

            await edgeOperator.EdgeDeploymentOnEventHandlerAsync(WatchEventType.Added, edgeDefinition);
            await edgeOperator.EdgeDeploymentOnEventHandlerAsync(WatchEventType.Modified, edgeDefinition);

            Mock.Get(controller).Verify(c => c.DeployModulesAsync(It.IsAny<ModuleSet>(), It.IsAny<ModuleSet>()), Times.Exactly(2));
            Mock.Get(client).Verify(c => c.ReplaceNamespacedCustomObjectStatusWithHttpMessagesAsync(It.IsAny<object>(), Constants.EdgeDeployment.Group, Constants.EdgeDeployment.Version, DeviceNamespace, Constants.EdgeDeployment.Plural, It.IsAny<string>(), null, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async void NoProcessingDeploymentOnError()
        {
            var edgeDefinition = new EdgeDeploymentDefinition("v1", "EdgeDeployment", new V1ObjectMeta(name: ResourceName), new List<KubernetesModule>(), null);

            var client = Mock.Of<IKubernetes>();
            var controller = Mock.Of<IEdgeDeploymentController>();

            var edgeOperator = new EdgeDeploymentOperator(
                ResourceName,
                DeviceNamespace,
                client,
                controller);

            await edgeOperator.EdgeDeploymentOnEventHandlerAsync(WatchEventType.Error, edgeDefinition);

            Mock.Get(controller).VerifyAll();
            Mock.Get(client).VerifyAll();
        }

        [Fact]
        public async void NoProcessingDeploymentIfEdgeDeploymentNameMismatch()
        {
            var edgeDefinition = new EdgeDeploymentDefinition("v1", "EdgeDeployment", new V1ObjectMeta(name: "not-the-resource-name"), new List<KubernetesModule>(), null);

            var client = Mock.Of<IKubernetes>();
            var controller = Mock.Of<IEdgeDeploymentController>();

            var edgeOperator = new EdgeDeploymentOperator(
                ResourceName,
                DeviceNamespace,
                client,
                controller);

            await edgeOperator.EdgeDeploymentOnEventHandlerAsync(WatchEventType.Added, edgeDefinition);
            Mock.Get(controller).VerifyAll();
            Mock.Get(client).VerifyAll();
        }

        [Fact]
        public async void StatusIsFailedWhenUnexpectedExceptionIsThrown()
        {
            Exception controllerException = new Exception(ExceptionMessage);
            EdgeDeploymentStatus expectedStatus = EdgeDeploymentStatus.Failure(controllerException);
            Option<EdgeDeploymentStatus> reportedStatus = Option.None<EdgeDeploymentStatus>();
            var edgeDefinition = new EdgeDeploymentDefinition("v1", "EdgeDeployment", new V1ObjectMeta(name: ResourceName), new List<KubernetesModule>(), null);
            var response = new HttpOperationResponse<object>()
            {
                Body = edgeDefinition,
                Response = new HttpResponseMessage(HttpStatusCode.OK)
            };

            var controller = Mock.Of<IEdgeDeploymentController>();
            Mock.Get(controller).Setup(c => c.DeployModulesAsync(It.IsAny<ModuleSet>(), It.IsAny<ModuleSet>()))
                .ThrowsAsync(controllerException);

            var client = Mock.Of<IKubernetes>();
            Mock.Get(client).Setup(c => c.ReplaceNamespacedCustomObjectStatusWithHttpMessagesAsync(It.IsAny<object>(), Constants.EdgeDeployment.Group, Constants.EdgeDeployment.Version, DeviceNamespace, Constants.EdgeDeployment.Plural, It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
                .Callback((object o, string group, string version, string _namespace, string plural, string name, Dictionary<string, List<string>> headers, CancellationToken token) =>
                {
                    Assert.True(o is JObject);
                    EdgeDeploymentDefinition e = ((JObject)o).ToObject<EdgeDeploymentDefinition>();
                    reportedStatus = e.Status;
                })
                .ReturnsAsync(response);

            var edgeOperator = new EdgeDeploymentOperator(
                ResourceName,
                DeviceNamespace,
                client,
                controller);

            Exception ex = await Assert.ThrowsAsync<Exception>(() => edgeOperator.EdgeDeploymentOnEventHandlerAsync(WatchEventType.Added, edgeDefinition));
            Assert.Equal(controllerException, ex);
            Assert.True(reportedStatus.HasValue);
            Assert.Equal(expectedStatus, reportedStatus.OrDefault());
            Mock.Get(controller).VerifyAll();
            Mock.Get(client).VerifyAll();
        }

        [Fact]
        public async void StatusIsFailedWithSpecialMessageWhenHttpExceptionIsThrown()
        {
            HttpOperationException controllerException = new HttpOperationException(ExceptionMessage)
                {
                    Request = new HttpRequestMessageWrapper(new HttpRequestMessage(HttpMethod.Put, new Uri("http://valid-uri")), "content")
                };
            Option<EdgeDeploymentStatus> reportedStatus = Option.None<EdgeDeploymentStatus>();
            EdgeDeploymentStatus expectedStatus = new EdgeDeploymentStatus(EdgeDeploymentStatusType.Failure, $"{controllerException.Request.Method} [{controllerException.Request.RequestUri}]({controllerException.Message})");
            var edgeDefinition = new EdgeDeploymentDefinition("v1", "EdgeDeployment", new V1ObjectMeta(name: ResourceName), new List<KubernetesModule>(), null);
            var response = new HttpOperationResponse<object>()
            {
                Body = edgeDefinition,
                Response = new HttpResponseMessage(HttpStatusCode.OK)
            };

            var controller = Mock.Of<IEdgeDeploymentController>();
            Mock.Get(controller).Setup(c => c.DeployModulesAsync(It.IsAny<ModuleSet>(), It.IsAny<ModuleSet>()))
                .ThrowsAsync(controllerException);

            var client = Mock.Of<IKubernetes>();
            Mock.Get(client).Setup(c => c.ReplaceNamespacedCustomObjectStatusWithHttpMessagesAsync(It.IsAny<object>(), Constants.EdgeDeployment.Group, Constants.EdgeDeployment.Version, DeviceNamespace, Constants.EdgeDeployment.Plural, It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
                .Callback((object o, string group, string version, string _namespace, string plural, string name, Dictionary<string, List<string>> headers, CancellationToken token) =>
                {
                    Assert.True(o is JObject);
                    EdgeDeploymentDefinition e = ((JObject)o).ToObject<EdgeDeploymentDefinition>();
                    reportedStatus = e.Status;
                })
                .ReturnsAsync(response);

            var edgeOperator = new EdgeDeploymentOperator(
                ResourceName,
                DeviceNamespace,
                client,
                controller);

            HttpOperationException ex = await Assert.ThrowsAsync<HttpOperationException>(() => edgeOperator.EdgeDeploymentOnEventHandlerAsync(WatchEventType.Added, edgeDefinition));
            Assert.Equal(controllerException, ex);
            Assert.True(reportedStatus.HasValue);
            Assert.Equal(expectedStatus, reportedStatus.OrDefault());
            Mock.Get(controller).VerifyAll();
            Mock.Get(client).VerifyAll();
        }
    }
}
