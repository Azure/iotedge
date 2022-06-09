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
    using Microsoft.Azure.Devices.Edge.Hub.Http;
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
        // [SuppressMessage("Microsoft.Security", "CS002:SecretInNextLine", Justification="Synthetic symmetric keys used in tests")]
        readonly string primaryKey = "t3LtII3CppvtVqycKp9bo043vCEgWbGBJAzXZNmoBXo=";

        // [SuppressMessage("Microsoft.Security", "CS002:SecretInNextLine", Justification="Synthetic symmetric keys used in tests")]
        readonly string secondaryKey = "kT4ac4PpH5UY0vA1JpLQWOu2yG6qKoqwvzee3j1Z3bA=";

        [Fact]
        public async Task GetDevicesAndModulesInTargetDeviceScope_RoundTripTest()
        {
            // Setup ServiceIdentity results
            string parentEdgeId = "edge1";
            string childEdgeId = "edge2";
            string deviceId = "device1";
            string moduleId = "module1";
            string deviceScope = "deviceScope1";
            string parentScope = "parentScope1";
            string generationId = "generation1";
            var authentication = new ServiceAuthentication(new SymmetricKeyAuthentication(this.primaryKey, this.secondaryKey));
            var resultDeviceIdentity = new ServiceIdentity(deviceId, null, deviceScope, new List<string>() { parentScope }, generationId, Enumerable.Empty<string>(), authentication, ServiceIdentityStatus.Enabled);
            var resultModuleIdentity = new ServiceIdentity(deviceId, moduleId, null, new List<string>(), generationId, Enumerable.Empty<string>(), authentication, ServiceIdentityStatus.Enabled);
            var resultIdentities = new List<ServiceIdentity>() { resultDeviceIdentity, resultModuleIdentity };
            var authChainMapping = new Dictionary<string, string>();
            authChainMapping.Add(childEdgeId, "edge2;edge1;edgeroot");
            var controller = MakeController(childEdgeId, resultIdentities, authChainMapping);

            // Act
            var request = new NestedScopeRequest(0, string.Empty, "edge2;edge1");
            await controller.GetDevicesAndModulesInTargetDeviceScopeAsync(parentEdgeId, "$edgeHub", request);

            // Verify EdgeHub result types
            var expectedAuth = new AuthenticationMechanism() { SymmetricKey = new SymmetricKey() { PrimaryKey = this.primaryKey, SecondaryKey = this.secondaryKey } };
            var expectedDeviceIdentities = new List<EdgeHubScopeDevice>() { new EdgeHubScopeDevice(deviceId, generationId, DeviceStatus.Enabled, expectedAuth, new DeviceCapabilities(), deviceScope, new List<string> { parentScope }) };
            var expectedModuleIdentities = new List<EdgeHubScopeModule>() { new EdgeHubScopeModule(moduleId, deviceId, generationId, expectedAuth) };
            var responseExpected = new EdgeHubScopeResultSuccess(expectedDeviceIdentities, expectedModuleIdentities);
            var responseExpectedJson = JsonConvert.SerializeObject(responseExpected);

            var responseActualBytes = GetResponseBodyBytes(controller);
            var responseActualJson = Encoding.UTF8.GetString(responseActualBytes);

            Assert.Equal((int)HttpStatusCode.OK, controller.HttpContext.Response.StatusCode);
            Assert.Equal(responseExpectedJson, responseActualJson);

            // Deserialize JSON back to SDK result types
            var scopeResult = JsonConvert.DeserializeObject<ScopeResult>(responseActualJson);

            // Convert to original ServiceIdentity type
            Assert.Equal(1, (int)scopeResult.Devices.Count());
            Assert.Equal(1, (int)scopeResult.Modules.Count());
            ServiceIdentity device = scopeResult.Devices.First().ToServiceIdentity();
            ServiceIdentity module = scopeResult.Modules.First().ToServiceIdentity();
            Assert.Equal(resultDeviceIdentity, device);
            Assert.Equal(resultModuleIdentity, module);
        }

        [Fact]
        public async Task GetModuleOnBehalfOf()
        {
            // Setup ServiceIdentity results
            string parentEdgeId = "edge1";
            string childEdgeId = "edge2";
            string moduleId = "module1";
            string deviceScope = "deviceScope1";
            string parentScope = "parentScope1";
            string generationId = "generation1";
            var authentication = new ServiceAuthentication(new SymmetricKeyAuthentication(this.primaryKey, this.secondaryKey));
            var resultDeviceIdentity = new ServiceIdentity(childEdgeId, null, deviceScope, new List<string>() { parentScope }, generationId, Enumerable.Empty<string>(), authentication, ServiceIdentityStatus.Enabled);
            var resultModuleIdentity = new ServiceIdentity(childEdgeId, moduleId, null, new List<string>() { deviceScope }, generationId, Enumerable.Empty<string>(), authentication, ServiceIdentityStatus.Enabled);
            var resultIdentities = new List<ServiceIdentity>() { resultDeviceIdentity, resultModuleIdentity };
            var authChainMapping = new Dictionary<string, string>();
            string targetId = childEdgeId + "/" + moduleId;
            authChainMapping.Add(targetId, $"{targetId};{childEdgeId};{parentEdgeId};edgeroot");
            var controller = MakeController(childEdgeId, resultIdentities, authChainMapping);

            // Act
            var request = new IdentityOnBehalfOfRequest(childEdgeId, moduleId, $"{childEdgeId};{parentEdgeId}");
            await controller.GetDeviceAndModuleOnBehalfOfAsync(parentEdgeId, "$edgeHub", request);

            // Verify EdgeHub result types
            var expectedAuth = new AuthenticationMechanism() { SymmetricKey = new SymmetricKey() { PrimaryKey = this.primaryKey, SecondaryKey = this.secondaryKey } };
            var expectedDeviceIdentities = new List<EdgeHubScopeDevice>() { new EdgeHubScopeDevice(childEdgeId, generationId, DeviceStatus.Enabled, expectedAuth, new DeviceCapabilities(), deviceScope, new List<string> { parentScope }) };
            var expectedModuleIdentities = new List<EdgeHubScopeModule>() { new EdgeHubScopeModule(moduleId, childEdgeId, generationId, expectedAuth) };
            var responseExpected = new EdgeHubScopeResultSuccess(expectedDeviceIdentities, expectedModuleIdentities);
            var responseExpectedJson = JsonConvert.SerializeObject(responseExpected);

            var responseActualBytes = GetResponseBodyBytes(controller);
            var responseActualJson = Encoding.UTF8.GetString(responseActualBytes);

            Assert.Equal((int)HttpStatusCode.OK, controller.HttpContext.Response.StatusCode);
            Assert.Equal(responseExpectedJson, responseActualJson);
        }

        [Fact]
        public async Task GetDeviceOnBehalfOf()
        {
            // Setup ServiceIdentity results
            string parentEdgeId = "edge1";
            string childEdgeId = "edge2";
            string deviceId = "device1";
            string moduleId = "module1";
            string deviceScope = "deviceScope1";
            string parentScope = "parentScope1";
            string generationId = "generation1";
            var authentication = new ServiceAuthentication(new SymmetricKeyAuthentication(this.primaryKey, this.secondaryKey));
            var resultDeviceIdentity = new ServiceIdentity(deviceId, null, deviceScope, new List<string>() { parentScope }, generationId, Enumerable.Empty<string>(), authentication, ServiceIdentityStatus.Enabled);
            var resultModuleIdentity = new ServiceIdentity(deviceId, moduleId, null, new List<string>() { deviceScope }, generationId, Enumerable.Empty<string>(), authentication, ServiceIdentityStatus.Enabled);
            var resultIdentities = new List<ServiceIdentity>() { resultDeviceIdentity, resultModuleIdentity };
            var authChainMapping = new Dictionary<string, string>();
            authChainMapping.Add(deviceId, $"{deviceId};{childEdgeId};{parentEdgeId};edgeroot");
            var controller = MakeController(childEdgeId, resultIdentities, authChainMapping);

            // Act
            var request = new IdentityOnBehalfOfRequest(deviceId, null, $"{deviceId};{childEdgeId};{parentEdgeId}");
            await controller.GetDeviceAndModuleOnBehalfOfAsync(parentEdgeId, "$edgeHub", request);

            // Verify EdgeHub result types
            var expectedAuth = new AuthenticationMechanism() { SymmetricKey = new SymmetricKey() { PrimaryKey = this.primaryKey, SecondaryKey = this.secondaryKey } };
            var expectedDeviceIdentities = new List<EdgeHubScopeDevice>() { new EdgeHubScopeDevice(deviceId, generationId, DeviceStatus.Enabled, expectedAuth, new DeviceCapabilities(), deviceScope, new List<string> { parentScope }) };
            var responseExpected = new EdgeHubScopeResultSuccess(expectedDeviceIdentities, new List<EdgeHubScopeModule>());
            var responseExpectedJson = JsonConvert.SerializeObject(responseExpected);

            var responseActualBytes = GetResponseBodyBytes(controller);
            var responseActualJson = Encoding.UTF8.GetString(responseActualBytes);

            Assert.Equal((int)HttpStatusCode.OK, controller.HttpContext.Response.StatusCode);
            Assert.Equal(responseExpectedJson, responseActualJson);
        }

        [Fact]
        public void ValidateChainTest()
        {
            // Correct case
            Assert.True(AuthChainHelpers.ValidateAuthChain("edge1", "leaf1", "leaf1;edge1;edgeRoot"));

            // Unauthorized actor
            Assert.False(AuthChainHelpers.ValidateAuthChain("edge1", "leaf1", "leaf1;edge2;edgeRoot"));

            // Bad target
            Assert.False(AuthChainHelpers.ValidateAuthChain("edge1", "leaf1", "leaf2;edge1;edgeRoot"));

            // Invalid format
            Assert.False(AuthChainHelpers.ValidateAuthChain("edge1", "leaf1", ";"));
        }

        [Theory]
        [InlineData("l4", "l3", "l3;l4;l5", true)]
        [InlineData("l4", "l4", "l4;l5", true)]
        [InlineData("l4", "l2", "l2;l3;l4;l5", true)]
        [InlineData("l3", "l2", "l2;l3;l4;l5", true)]
        [InlineData("l3-2", "l2", "l2;l3;l4;l5", false)]
        [InlineData("l5", "l2", "l2;l3;l4;l5", true)]
        public void ValidateAuthChainForRequestorTest(string actorDeviceId, string targetDeviceId, string authChain, bool expectedResult)
        {
            // Act
            (bool result, string _) = DeviceScopeController.ValidateAuthChainForRequestor(actorDeviceId, targetDeviceId, Option.Maybe(authChain));

            // Verify
            Assert.Equal(expectedResult, result);
        }

        [Theory]
        [InlineData("l4", "$edgeHub", "l3;l4", "l3", "l3;l4;l5", HttpStatusCode.OK)]
        [InlineData("l4", "$edgeHub", "l2;l3;l4", "l2", "l2;l3;l4;l5", HttpStatusCode.OK)]
        [InlineData("l3", "$edgeHub", "l2;l3", "l2", "l2;l3;l4;l5", HttpStatusCode.OK)]
        [InlineData("l3", "$edgeHub", "l4", "l4", "l4;l5", HttpStatusCode.Unauthorized)]
        [InlineData("l3", "$edgeHub", "l4", "l4", "l5", HttpStatusCode.Unauthorized)]
        public async Task HandleDevicesAndModulesInTargetDeviceScopeAsyncTest(
            string actorDeviceId,
            string actorModuleId,
            string authChain,
            string targetDeviceId,
            string authChainToTarget,
            HttpStatusCode expectedStatus)
        {
            // Setup
            var request = new NestedScopeRequest(1, string.Empty, authChain);
            var identitiesCache = new Mock<IDeviceScopeIdentitiesCache>();
            identitiesCache.Setup(i => i.GetAuthChain(targetDeviceId)).Returns(Task.FromResult(Option.Some(authChainToTarget)));
            identitiesCache.Setup(i => i.GetServiceIdentity(targetDeviceId)).Returns(Task.FromResult(Option.None<ServiceIdentity>()));
            identitiesCache.Setup(i => i.GetDevicesAndModulesInTargetScopeAsync(targetDeviceId)).Returns(Task.FromResult(new List<ServiceIdentity>() as IList<ServiceIdentity>));

            // Act
            EdgeHubScopeResult result = await DeviceScopeController.HandleDevicesAndModulesInTargetDeviceScopeAsync(actorDeviceId, actorModuleId, request, identitiesCache.Object);

            // Verify
            Assert.Equal(expectedStatus, result.Status);
        }

        [Theory]
        [InlineData("l4", "$edgeHub", "leaf1", null, "l3;l4", "l3", "leaf1;l3;l4;l5", HttpStatusCode.OK)]
        [InlineData("l4", "$edgeHub", "leaf1", "leafmod", "l3;l4", "l3", "leaf1/leafmod;leaf1;l3;l4;l5", HttpStatusCode.OK)]
        [InlineData("l4", "$edgeHub", "leaf1", null, "l4", "l4", "leaf1;l3;l4;l5", HttpStatusCode.OK)]
        [InlineData("l3", "$edgeHub", "leaf1", "leafmod", "l3;l4;l5", "l3", "leaf1/leafmod;leaf1;l3;l4;l5", HttpStatusCode.OK)]
        [InlineData("l3", "$edgeHub", "l4", "mod1", "l3;l4;l5", "l3", "l4/mod1;l4;l5", HttpStatusCode.Unauthorized)]
        [InlineData("l3", "$edgeHub", "l4child", null, "l3;l4", "l3", "l4child;l4", HttpStatusCode.Unauthorized)]
        [InlineData("l3", "$edgeHub", "l4child", null, "l3;l4l;5", "l3", "l4child;l4;l5", HttpStatusCode.Unauthorized)]
        [InlineData("l3", "$edgeHub", "l4", null, "l3;l4", "l3", "l4", HttpStatusCode.Unauthorized)]
        public async Task HandleGetDeviceAndModuleOnBehalfOfAsync(
            string actorDeviceId,
            string actorModuleId,
            string targetDeviceId,
            string targetModuleId,
            string authChain,
            string originatorDeviceId,
            string authChainToTarget,
            HttpStatusCode expectedStatus)
        {
            // Setup
            var request = new IdentityOnBehalfOfRequest(targetDeviceId, targetModuleId, authChain);
            var identitiesCache = new Mock<IDeviceScopeIdentitiesCache>();
            string targetId = targetDeviceId + (string.IsNullOrWhiteSpace(targetModuleId) ? string.Empty : $"/{targetModuleId}");
            identitiesCache.Setup(i => i.RefreshServiceIdentityOnBehalfOf(targetId, originatorDeviceId)).Returns(Task.CompletedTask);
            var targetServiceIdentity = new ServiceIdentity(targetId, "dummy", Enumerable.Empty<string>(), new ServiceAuthentication(ServiceAuthenticationType.None), ServiceIdentityStatus.Enabled);
            identitiesCache.Setup(i => i.GetServiceIdentity(targetId)).Returns(Task.FromResult(Option.Some(targetServiceIdentity)));
            identitiesCache.Setup(i => i.GetAuthChain(targetId)).Returns(Task.FromResult(Option.Some(authChainToTarget)));

            // Act
            EdgeHubScopeResult edgeHubScopeResult = await DeviceScopeController.HandleGetDeviceAndModuleOnBehalfOfAsync(actorDeviceId, actorModuleId, request, identitiesCache.Object);

            // Verity
            Assert.Equal(expectedStatus, edgeHubScopeResult.Status);
        }

        private static DeviceScopeController MakeController(string targetEdgeId, IList<ServiceIdentity> resultIdentities, IDictionary<string, string> authChains)
        {
            var identitiesCache = new Mock<IDeviceScopeIdentitiesCache>();
            var edgeHub = new Mock<IEdgeHub>();
            edgeHub.Setup(e => e.GetDeviceScopeIdentitiesCache())
                .Returns(identitiesCache.Object);

            identitiesCache.Setup(c => c.GetDevicesAndModulesInTargetScopeAsync(It.Is<string>(id => id == targetEdgeId)))
                .ReturnsAsync(resultIdentities);

            foreach (KeyValuePair<string, string> entry in authChains)
            {
                identitiesCache.Setup(c => c.GetAuthChain(It.Is<string>(i => i == entry.Key)))
                .ReturnsAsync(Option.Some<string>(entry.Value));
            }

            foreach (ServiceIdentity identity in resultIdentities)
            {
                identitiesCache.Setup(c => c.GetServiceIdentity(It.Is<string>(id => id == identity.Id)))
                .ReturnsAsync(Option.Some(identity));
            }

            var authenticator = new Mock<IHttpRequestAuthenticator>();
            authenticator.Setup(a => a.AuthenticateAsync(It.IsAny<string>(), It.IsAny<Option<string>>(), It.IsAny<Option<string>>(), It.IsAny<HttpContext>()))
                .ReturnsAsync(new HttpAuthResult(true, string.Empty));

            var controller = new DeviceScopeController(Task.FromResult(edgeHub.Object), Task.FromResult(authenticator.Object));
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
