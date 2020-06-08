// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Http.Test
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.Devices.Edge.Hub.CloudProxy;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity.Service;
    using Microsoft.Azure.Devices.Edge.Hub.Http.Controllers;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Shared;
    using Moq;
    using Newtonsoft.Json;
    using Xunit;

    [Unit]
    public class DeviceScopeControllerTest
    {
        static readonly string actorId = "edge1";
        static readonly string targetId = "edge2";
        static readonly string targetAuthChain = "edge2;edge1;edgeroot";

        [Fact]
        public async Task InvokeTestSuccess()
        {
            string deviceId = "device1";
            string moduleId = "module1";
            string deviceScope = "deviceScope1";
            string parentScope = "parentScope1";
            string generationId = "generation1";
            string primaryKey = "t3LtII3CppvtVqycKp9bo043vCEgWbGBJAzXZNmoBXo=";
            string secondaryKey = "kT4ac4PpH5UY0vA1JpLQWOu2yG6qKoqwvzee3j1Z3bA=";
            var authentication = new ServiceAuthentication(new SymmetricKeyAuthentication(primaryKey, secondaryKey));
            var resultDeviceIdentity = new ServiceIdentity(deviceId, null, deviceScope, new List<string>() { parentScope }, generationId, Enumerable.Empty<string>(), authentication, ServiceIdentityStatus.Enabled);
            var resultModuleIdentity = new ServiceIdentity(deviceId, moduleId, null, new List<string>() { deviceScope }, generationId, Enumerable.Empty<string>(), authentication, ServiceIdentityStatus.Enabled);
            var resultIdentities = new List<ServiceIdentity>() { resultDeviceIdentity, resultModuleIdentity };
            var controller = MakeController(resultIdentities);

            var request = new NestedScopeRequest(0, string.Empty, "edge2;edge1");
            await controller.InvokeAsync(actorId, "$edgeHub", request);

            var expectedAuth = new AuthenticationMechanism() { SymmetricKey = new SymmetricKey() { PrimaryKey = primaryKey, SecondaryKey = secondaryKey } };
            var expectedDeviceIdentities = new List<EdgeHubScopeDevice>() { new EdgeHubScopeDevice(deviceId, generationId, DeviceStatus.Enabled, expectedAuth, new DeviceCapabilities(), deviceScope, new List<string> { parentScope }) };
            var expectedModuleIdentities = new List<EdgeHubScopeModule>() { new EdgeHubScopeModule(moduleId, deviceId, generationId, expectedAuth) };
            var responseExpected = new EdgeHubScopeResult() { Devices = expectedDeviceIdentities, Modules = expectedModuleIdentities };
            var responseExpectedJson = JsonConvert.SerializeObject(responseExpected);

            var responseActualBytes = GetResponseBodyBytes(controller);
            var responseActualJson = Encoding.UTF8.GetString(responseActualBytes);

            Assert.Equal((int)HttpStatusCode.OK, controller.HttpContext.Response.StatusCode);
            Assert.Equal(responseExpectedJson, responseActualJson);
        }

        [Fact]
        public async void ValidateChainAndGetTargetDeviceIdTest()
        {
            var resultIdentities = new List<ServiceIdentity>();
            var controller = MakeController(resultIdentities);

            var request = new NestedScopeRequest(0, string.Empty, "edge2;edge1");
            Assert.Throws<AggregateException>(() => controller.InvokeAsync("edge1", "notEdgeHub", request).Wait());

            request = new NestedScopeRequest(0, string.Empty, "edge2;edge2");
            await controller.InvokeAsync("edge1", "$edgeHub", request);
            Assert.Equal((int)HttpStatusCode.BadRequest, controller.HttpContext.Response.StatusCode);
        }

        private static DeviceScopeController MakeController(IList<ServiceIdentity> resultIdentities)
        {
            var edgeHub = new Mock<IEdgeHub>();
            edgeHub.Setup(e => e.GetAuthChainForIdentity(It.Is<string>(i => i == targetId)))
                .ReturnsAsync(Option.Some<string>(targetAuthChain));
            edgeHub.Setup(e => e.GetDevicesAndModulesInTargetScopeAsync(It.Is<string>(id => id == targetId)))
                .ReturnsAsync(resultIdentities);

            var controller = new DeviceScopeController(Task.FromResult(edgeHub.Object));
            SetupControllerContext(controller);

            return controller;
        }

        private static void SetupControllerContext(Controller controller)
        {
            var httpContext = new DefaultHttpContext();
            var httpResponse = new DefaultHttpResponse(httpContext);
            httpResponse.Body = new MemoryStream();
            var controllerContext = new ControllerContext();
            controllerContext.HttpContext = httpContext;
            controller.ControllerContext = controllerContext;
        }

        private static byte[] GetResponseBodyBytes(Controller controller)
        {
            return (controller.HttpContext.Response.Body as MemoryStream).ToArray();
        }
    }
}
