// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Http.Test
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Http.Controllers;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Newtonsoft.Json;
    using Xunit;

    [Unit]
    public class RegistryControllerTest
    {
        const string ChildEdgeId = "childEdge";
        const string ParentEdgeId = "parentEdge";
        const string RootEdgeId = "rootEdge";
        const string ModuleId = "module1";
        const string AuthChainOnParent = "childEdge;parentEdge";
        const string AuthChainOnRoot = "childEdge;parentEdge;rootEdge";

        [Fact]
        public async Task CreateOrUpdateModule_Success()
        {
            // Setup
            var authChainMapping = new Dictionary<string, string>()
            {
                { ChildEdgeId, AuthChainOnParent }
            };
            (RegistryController controller, Mock<IRegistryOnBehalfOfApiClient> registryApiClient, Mock<IHttpRequestAuthenticator> authenticator, Mock<IDeviceScopeIdentitiesCache> _) =
                this.TestSetup(ParentEdgeId, authChainMapping);
            CreateOrUpdateModuleOnBehalfOfData actualRequestData = null;
            var module = new Module(ChildEdgeId, ModuleId);
            registryApiClient.Setup(c => c.PutModuleAsync(ParentEdgeId, It.IsAny<CreateOrUpdateModuleOnBehalfOfData>(), It.IsAny<string>()))
                .Callback<string, CreateOrUpdateModuleOnBehalfOfData, string>((_, data, __) => actualRequestData = data)
                .Returns(Task.FromResult(new RegistryApiHttpResult(HttpStatusCode.OK, JsonConvert.SerializeObject(module))));
            authenticator.Setup(a => a.AuthenticateAsync(ChildEdgeId, Option.None<string>(), Option.None<string>(), It.IsAny<HttpContext>()))
                .ReturnsAsync(new HttpAuthResult(true, string.Empty));

            // Act
            await controller.CreateOrUpdateModuleAsync(ChildEdgeId, ModuleId, "*", module);

            // Verify
            registryApiClient.Verify(c => c.PutModuleAsync(ParentEdgeId, It.IsAny<CreateOrUpdateModuleOnBehalfOfData>(), It.IsAny<string>()), Times.Once());
            Assert.Equal(AuthChainOnParent, actualRequestData.AuthChain);
            Assert.Equal(ChildEdgeId, actualRequestData.Module.DeviceId);
            Assert.Equal(ModuleId, actualRequestData.Module.Id);
            Assert.Equal((int)HttpStatusCode.OK, controller.HttpContext.Response.StatusCode);

            byte[] actualResponseBytes = this.GetResponseBodyBytes(controller);
            string actualResponseJson = Encoding.UTF8.GetString(actualResponseBytes);
            Module actualModule = JsonConvert.DeserializeObject<Module>(actualResponseJson);
            Assert.Equal(ChildEdgeId, actualModule.DeviceId);
            Assert.Equal(ModuleId, actualModule.Id);
        }

        [Fact]
        public async Task GetModule_Success()
        {
            // Setup
            var authChainMapping = new Dictionary<string, string>()
            {
                { ChildEdgeId, AuthChainOnParent }
            };
            (RegistryController controller, Mock<IRegistryOnBehalfOfApiClient> registryApiClient, Mock<IHttpRequestAuthenticator> authenticator, Mock<IDeviceScopeIdentitiesCache> _) =
                this.TestSetup(ParentEdgeId, authChainMapping);
            GetModuleOnBehalfOfData actualRequestData = null;
            var module = new Module(ChildEdgeId, ModuleId);
            registryApiClient.Setup(c => c.GetModuleAsync(ParentEdgeId, It.IsAny<GetModuleOnBehalfOfData>()))
                .Callback<string, GetModuleOnBehalfOfData>((_, data) => actualRequestData = data)
                .Returns(Task.FromResult(new RegistryApiHttpResult(HttpStatusCode.OK, JsonConvert.SerializeObject(module))));
            authenticator.Setup(a => a.AuthenticateAsync(ChildEdgeId, Option.None<string>(), Option.None<string>(), It.IsAny<HttpContext>()))
                .ReturnsAsync(new HttpAuthResult(true, string.Empty));

            // Act
            await controller.GetModuleAsync(ChildEdgeId, ModuleId);

            // Verify
            registryApiClient.Verify(c => c.GetModuleAsync(ParentEdgeId, It.IsAny<GetModuleOnBehalfOfData>()), Times.Once());
            Assert.Equal(AuthChainOnParent, actualRequestData.AuthChain);
            Assert.Equal(ModuleId, actualRequestData.ModuleId);
            Assert.Equal((int)HttpStatusCode.OK, controller.HttpContext.Response.StatusCode);

            byte[] actualResponseBytes = this.GetResponseBodyBytes(controller);
            string actualResponseJson = Encoding.UTF8.GetString(actualResponseBytes);
            Module actualModule = JsonConvert.DeserializeObject<Module>(actualResponseJson);
            Assert.Equal(ChildEdgeId, actualModule.DeviceId);
            Assert.Equal(ModuleId, actualModule.Id);
        }

        [Fact]
        public async Task ListModules_Success()
        {
            // Setup
            var authChainMapping = new Dictionary<string, string>()
            {
                { ChildEdgeId, AuthChainOnParent }
            };
            (RegistryController controller, Mock<IRegistryOnBehalfOfApiClient> registryApiClient, Mock<IHttpRequestAuthenticator> authenticator, Mock<IDeviceScopeIdentitiesCache> _) =
                this.TestSetup(ParentEdgeId, authChainMapping);
            ListModulesOnBehalfOfData actualRequestData = null;
            var modules = new[] { new Module(ChildEdgeId, "moduleId2"), new Module(ChildEdgeId, "moduleId1") };
            registryApiClient.Setup(c => c.ListModulesAsync(ParentEdgeId, It.IsAny<ListModulesOnBehalfOfData>()))
                .Callback<string, ListModulesOnBehalfOfData>((_, data) => actualRequestData = data)
                .Returns(Task.FromResult(new RegistryApiHttpResult(HttpStatusCode.OK, JsonConvert.SerializeObject(modules))));
            authenticator.Setup(a => a.AuthenticateAsync(ChildEdgeId, Option.None<string>(), Option.None<string>(), It.IsAny<HttpContext>()))
                .ReturnsAsync(new HttpAuthResult(true, string.Empty));

            // Act
            await controller.ListModulesAsync(ChildEdgeId);

            // Verify
            registryApiClient.Verify(c => c.ListModulesAsync(ParentEdgeId, It.IsAny<ListModulesOnBehalfOfData>()), Times.Once());
            Assert.Equal(AuthChainOnParent, actualRequestData.AuthChain);
            Assert.Equal((int)HttpStatusCode.OK, controller.HttpContext.Response.StatusCode);

            byte[] actualResponseBytes = this.GetResponseBodyBytes(controller);
            string actualResponseJson = Encoding.UTF8.GetString(actualResponseBytes);
            Module[] actualModules = JsonConvert.DeserializeObject<Module[]>(actualResponseJson);
            Assert.Equal(2, actualModules.Length);
            Assert.Equal(ChildEdgeId, actualModules[0].DeviceId);
            Assert.Contains(actualModules, m => m.Id.Equals("moduleId1"));
            Assert.Equal(ChildEdgeId, actualModules[1].DeviceId);
            Assert.Contains(actualModules, m => m.Id.Equals("moduleId2"));
        }

        [Fact]
        public async Task DeleteModule_Success()
        {
            // Setup
            var authChainMapping = new Dictionary<string, string>()
            {
                { ChildEdgeId, AuthChainOnParent }
            };
            (RegistryController controller, Mock<IRegistryOnBehalfOfApiClient> registryApiClient, Mock<IHttpRequestAuthenticator> authenticator, Mock<IDeviceScopeIdentitiesCache> _) =
                this.TestSetup(ParentEdgeId, authChainMapping);
            DeleteModuleOnBehalfOfData actualRequestData = null;
            var module = new Module(ChildEdgeId, ModuleId);
            registryApiClient.Setup(c => c.DeleteModuleAsync(ParentEdgeId, It.IsAny<DeleteModuleOnBehalfOfData>()))
                .Callback<string, DeleteModuleOnBehalfOfData>((_, data) => actualRequestData = data)
                .Returns(Task.FromResult(new RegistryApiHttpResult(HttpStatusCode.OK, string.Empty)));
            authenticator.Setup(a => a.AuthenticateAsync(ChildEdgeId, Option.None<string>(), Option.None<string>(), It.IsAny<HttpContext>()))
                .ReturnsAsync(new HttpAuthResult(true, string.Empty));

            // Act
            await controller.DeleteModuleAsync(ChildEdgeId, ModuleId);

            // Verify
            registryApiClient.Verify(c => c.DeleteModuleAsync(ParentEdgeId, It.IsAny<DeleteModuleOnBehalfOfData>()), Times.Once());
            Assert.Equal(AuthChainOnParent, actualRequestData.AuthChain);
            Assert.Equal(ModuleId, actualRequestData.ModuleId);
            Assert.Equal((int)HttpStatusCode.OK, controller.HttpContext.Response.StatusCode);

            byte[] actualResponseBytes = this.GetResponseBodyBytes(controller);
            Assert.Empty(actualResponseBytes);
        }

        [Fact]
        public async Task CreateOrUpdateModuleOnBehalfOf_Success()
        {
            // Setup
            var authChainMapping = new Dictionary<string, string>()
            {
                { ChildEdgeId, AuthChainOnRoot }
            };
            (RegistryController controller, Mock<IRegistryOnBehalfOfApiClient> registryApiClient, Mock<IHttpRequestAuthenticator> authenticator, Mock<IDeviceScopeIdentitiesCache> _) =
                this.TestSetup(RootEdgeId, authChainMapping);
            CreateOrUpdateModuleOnBehalfOfData actualRequestData = null;
            var module = new Module(ChildEdgeId, ModuleId);
            registryApiClient.Setup(c => c.PutModuleAsync(RootEdgeId, It.IsAny<CreateOrUpdateModuleOnBehalfOfData>(), It.IsAny<string>()))
                .Callback<string, CreateOrUpdateModuleOnBehalfOfData, string>((_, data, __) => actualRequestData = data)
                .Returns(Task.FromResult(new RegistryApiHttpResult(HttpStatusCode.OK, JsonConvert.SerializeObject(module))));
            authenticator.Setup(a => a.AuthenticateAsync(ParentEdgeId, Option.Some(Constants.EdgeHubModuleId), Option.Some(AuthChainOnParent), It.IsAny<HttpContext>()))
                .ReturnsAsync(new HttpAuthResult(true, string.Empty));

            // Act
            await controller.CreateOrUpdateModuleOnBehalfOfAsync(ParentEdgeId, "*", new CreateOrUpdateModuleOnBehalfOfData(AuthChainOnParent, module));

            // Verify
            registryApiClient.Verify(c => c.PutModuleAsync(RootEdgeId, It.IsAny<CreateOrUpdateModuleOnBehalfOfData>(), It.IsAny<string>()), Times.Once());
            Assert.Equal(AuthChainOnRoot, actualRequestData.AuthChain);
            Assert.Equal(ChildEdgeId, actualRequestData.Module.DeviceId);
            Assert.Equal(ModuleId, actualRequestData.Module.Id);
            Assert.Equal((int)HttpStatusCode.OK, controller.HttpContext.Response.StatusCode);

            byte[] actualResponseBytes = this.GetResponseBodyBytes(controller);
            string actualResponseJson = Encoding.UTF8.GetString(actualResponseBytes);
            Module actualModule = JsonConvert.DeserializeObject<Module>(actualResponseJson);
            Assert.Equal(ChildEdgeId, actualModule.DeviceId);
            Assert.Equal(ModuleId, actualModule.Id);
        }

        [Fact]
        public async Task GetModuleOnBehalfOf_Success()
        {
            // Setup
            var authChainMapping = new Dictionary<string, string>()
            {
                { ChildEdgeId, AuthChainOnRoot }
            };
            (RegistryController controller, Mock<IRegistryOnBehalfOfApiClient> registryApiClient, Mock<IHttpRequestAuthenticator> authenticator, Mock<IDeviceScopeIdentitiesCache> _) =
                this.TestSetup(RootEdgeId, authChainMapping);
            GetModuleOnBehalfOfData actualRequestData = null;
            var module = new Module(ChildEdgeId, ModuleId);
            registryApiClient.Setup(c => c.GetModuleAsync(RootEdgeId, It.IsAny<GetModuleOnBehalfOfData>()))
                .Callback<string, GetModuleOnBehalfOfData>((_, data) => actualRequestData = data)
                .Returns(Task.FromResult(new RegistryApiHttpResult(HttpStatusCode.OK, JsonConvert.SerializeObject(module))));
            authenticator.Setup(a => a.AuthenticateAsync(ParentEdgeId, Option.Some(Constants.EdgeHubModuleId), Option.Some(AuthChainOnParent), It.IsAny<HttpContext>()))
                .ReturnsAsync(new HttpAuthResult(true, string.Empty));

            // Act
            await controller.GetModuleOnBehalfOfAsync(ParentEdgeId, new GetModuleOnBehalfOfData(AuthChainOnParent, ModuleId));

            // Verify
            registryApiClient.Verify(c => c.GetModuleAsync(RootEdgeId, It.IsAny<GetModuleOnBehalfOfData>()), Times.Once());
            Assert.Equal(AuthChainOnRoot, actualRequestData.AuthChain);
            Assert.Equal(ModuleId, actualRequestData.ModuleId);
            Assert.Equal((int)HttpStatusCode.OK, controller.HttpContext.Response.StatusCode);

            byte[] actualResponseBytes = this.GetResponseBodyBytes(controller);
            string actualResponseJson = Encoding.UTF8.GetString(actualResponseBytes);
            Module actualModule = JsonConvert.DeserializeObject<Module>(actualResponseJson);
            Assert.Equal(ChildEdgeId, actualModule.DeviceId);
            Assert.Equal(ModuleId, actualModule.Id);
        }

        [Fact]
        public async Task ListModulesOnBehalfOf_Success()
        {
            // Setup
            var authChainMapping = new Dictionary<string, string>()
            {
                { ChildEdgeId, AuthChainOnRoot }
            };
            (RegistryController controller, Mock<IRegistryOnBehalfOfApiClient> registryApiClient, Mock<IHttpRequestAuthenticator> authenticator, Mock<IDeviceScopeIdentitiesCache> _) =
                this.TestSetup(RootEdgeId, authChainMapping);
            ListModulesOnBehalfOfData actualRequestData = null;
            var modules = new[] { new Module(ChildEdgeId, "moduleId2"), new Module(ChildEdgeId, "moduleId1") };
            registryApiClient.Setup(c => c.ListModulesAsync(RootEdgeId, It.IsAny<ListModulesOnBehalfOfData>()))
                .Callback<string, ListModulesOnBehalfOfData>((_, data) => actualRequestData = data)
                .Returns(Task.FromResult(new RegistryApiHttpResult(HttpStatusCode.OK, JsonConvert.SerializeObject(modules))));
            authenticator.Setup(a => a.AuthenticateAsync(ParentEdgeId, Option.Some(Constants.EdgeHubModuleId), Option.Some(AuthChainOnParent), It.IsAny<HttpContext>()))
                .ReturnsAsync(new HttpAuthResult(true, string.Empty));

            // Act
            await controller.ListModulesOnBehalfOfAsync(ParentEdgeId, new ListModulesOnBehalfOfData(AuthChainOnParent));

            // Verify
            registryApiClient.Verify(c => c.ListModulesAsync(RootEdgeId, It.IsAny<ListModulesOnBehalfOfData>()), Times.Once());
            Assert.Equal(AuthChainOnRoot, actualRequestData.AuthChain);
            Assert.Equal((int)HttpStatusCode.OK, controller.HttpContext.Response.StatusCode);

            byte[] actualResponseBytes = this.GetResponseBodyBytes(controller);
            string actualResponseJson = Encoding.UTF8.GetString(actualResponseBytes);
            Module[] actualModules = JsonConvert.DeserializeObject<Module[]>(actualResponseJson);
            Assert.Equal(2, actualModules.Length);
            Assert.Equal(ChildEdgeId, actualModules[0].DeviceId);
            Assert.Contains(actualModules, m => m.Id.Equals("moduleId1"));
            Assert.Equal(ChildEdgeId, actualModules[1].DeviceId);
            Assert.Contains(actualModules, m => m.Id.Equals("moduleId2"));
        }

        [Fact]
        public async Task DeleteModuleOnBehalfOf_Success()
        {
            // Setup
            var authChainMapping = new Dictionary<string, string>()
            {
                { ChildEdgeId, AuthChainOnRoot }
            };
            (RegistryController controller, Mock<IRegistryOnBehalfOfApiClient> registryApiClient, Mock<IHttpRequestAuthenticator> authenticator, Mock<IDeviceScopeIdentitiesCache> _) =
                this.TestSetup(RootEdgeId, authChainMapping);
            DeleteModuleOnBehalfOfData actualRequestData = null;
            registryApiClient.Setup(c => c.DeleteModuleAsync(RootEdgeId, It.IsAny<DeleteModuleOnBehalfOfData>()))
                .Callback<string, DeleteModuleOnBehalfOfData>((_, data) => actualRequestData = data)
                .Returns(Task.FromResult(new RegistryApiHttpResult(HttpStatusCode.OK, string.Empty)));
            authenticator.Setup(a => a.AuthenticateAsync(ParentEdgeId, Option.Some(Constants.EdgeHubModuleId), Option.Some(AuthChainOnParent), It.IsAny<HttpContext>()))
                .ReturnsAsync(new HttpAuthResult(true, string.Empty));

            // Act
            await controller.DeleteModuleOnBehalfOfAsync(ParentEdgeId, new DeleteModuleOnBehalfOfData(AuthChainOnParent, ModuleId));

            // Verify
            registryApiClient.Verify(c => c.DeleteModuleAsync(RootEdgeId, It.IsAny<DeleteModuleOnBehalfOfData>()), Times.Once());
            Assert.Equal(AuthChainOnRoot, actualRequestData.AuthChain);
            Assert.Equal(ModuleId, actualRequestData.ModuleId);
            Assert.Equal((int)HttpStatusCode.OK, controller.HttpContext.Response.StatusCode);

            byte[] actualResponseBytes = this.GetResponseBodyBytes(controller);
            Assert.Empty(actualResponseBytes);
        }

        [Theory]
        [InlineData("l4", "l3;l4", "l3", "l3;l4;l5", true, HttpStatusCode.OK)]
        [InlineData("l4", "l2;l3;l4", "l2", "l2;l3;l4;l5", true, HttpStatusCode.OK)]
        [InlineData("l4", "l4", "l4", "l4;l5", true, HttpStatusCode.OK)]
        [InlineData("l3", "l4", "l4", "l4;l5", true, HttpStatusCode.Unauthorized)]
        [InlineData("l4", "l3;l4", "l3", null, true, HttpStatusCode.Unauthorized)]
        [InlineData("l4", "l3;l4", "l3", "l3;l4;l5", false, HttpStatusCode.Unauthorized)]
        [InlineData("l4", "l3;l3", "l3", "l3;l4;l5", false, HttpStatusCode.Unauthorized)]
        public async Task ValidateOnBehalfOfCallTest(string actorDeviceId, string authChain, string targetDeviceId, string targetAuthChain, bool authResult, HttpStatusCode expectedResult)
        {
            // Setup
            var identitiesCache = Mock.Of<IDeviceScopeIdentitiesCache>(c => c.GetAuthChain(targetDeviceId) == Task.FromResult(Option.Maybe(targetAuthChain)));
            var edgeHub = Mock.Of<IEdgeHub>(e => e.GetDeviceScopeIdentitiesCache() == identitiesCache);
            var httpContext = new DefaultHttpContext();
            var httpRequestAuthenticator = Mock.Of<IHttpRequestAuthenticator>(a => a.AuthenticateAsync(actorDeviceId, Option.Some(Constants.EdgeHubModuleId), Option.Some(authChain), httpContext) == Task.FromResult(new HttpAuthResult(authResult, string.Empty)));

            // Act
            Try<string> authChainResult = await RegistryController.AuthorizeOnBehalfOf(actorDeviceId, authChain, "test", httpContext, edgeHub, httpRequestAuthenticator);

            // Verify
            if (expectedResult == HttpStatusCode.OK)
            {
                Assert.True(authChainResult.Success);
                Assert.Equal(targetAuthChain, authChainResult.Value);
            }
            else
            {
                Assert.False(authChainResult.Success);
                Assert.IsType<RegistryController.ValidationException>(authChainResult.Exception);
                Assert.Equal(expectedResult, ((RegistryController.ValidationException)authChainResult.Exception).StatusCode);
            }
        }

        [Theory]
        [MemberData(nameof(GetControllerMethodDelegates))]
        [System.Diagnostics.CodeAnalysis.SuppressMessage(
            "Usage",
            "xUnit1026:Theory methods should use all of their parameters",
            Justification = "For reuse in both AuthenticatedFail and AuthorizedFailed tests.")]
        public async Task AuthenticateFailed(Func<RegistryController, Task> funcDelegate, string _)
        {
            // Setup
            (RegistryController controller, Mock<IRegistryOnBehalfOfApiClient> _, Mock<IHttpRequestAuthenticator> _, Mock<IDeviceScopeIdentitiesCache> _) =
                this.TestSetup(ParentEdgeId, new Dictionary<string, string>());

            // Act
            await funcDelegate(controller);

            // Verify
            Assert.Equal((int)HttpStatusCode.Unauthorized, controller.HttpContext.Response.StatusCode);
        }

        [Theory]
        [MemberData(nameof(GetControllerMethodDelegates))]
        public async Task AuthorizeFailed(Func<RegistryController, Task> funcDelegate, string actorDeviceId)
        {
            // Setup
            (RegistryController controller, Mock<IRegistryOnBehalfOfApiClient> _, Mock<IHttpRequestAuthenticator> authenticator, Mock<IDeviceScopeIdentitiesCache> identitiesCache) =
                this.TestSetup(ParentEdgeId, new Dictionary<string, string>());
            authenticator.Setup(a => a.AuthenticateAsync(actorDeviceId, It.IsAny<Option<string>>(), It.IsAny<Option<string>>(), It.IsAny<HttpContext>()))
                .ReturnsAsync(new HttpAuthResult(true, string.Empty));

            // Act
            await funcDelegate(controller);

            // Verify
            identitiesCache.Verify(c => c.GetAuthChain(ChildEdgeId), Times.Once());
            Assert.Equal((int)HttpStatusCode.Unauthorized, controller.HttpContext.Response.StatusCode);
        }

        public static IEnumerable<object[]> GetControllerMethodDelegates()
        {
            var module = new Module(ChildEdgeId, ModuleId);

            Func<RegistryController, Task> createOrUpdateModuleFunc =
                (controller) => controller.CreateOrUpdateModuleAsync(ChildEdgeId, ModuleId, "*", module);
            Func<RegistryController, Task> getModuleFunc =
                (controller) => controller.GetModuleAsync(ChildEdgeId, ModuleId);
            Func<RegistryController, Task> listModulesFunc =
                (controller) => controller.ListModulesAsync(ChildEdgeId);
            Func<RegistryController, Task> deleteModuleFunc =
                (controller) => controller.DeleteModuleAsync(ChildEdgeId, ModuleId);
            Func<RegistryController, Task> createOrUpdateModuleOnBehalfOfFunc =
                (controller) => controller.CreateOrUpdateModuleOnBehalfOfAsync(ParentEdgeId, "*", new CreateOrUpdateModuleOnBehalfOfData(AuthChainOnParent, module));
            Func<RegistryController, Task> getModuleOnBehalfOfFunc =
                (controller) => controller.GetModuleOnBehalfOfAsync(ParentEdgeId, new GetModuleOnBehalfOfData(AuthChainOnParent, ModuleId));
            Func<RegistryController, Task> listModulesOnBehalfOfFunc =
                (controller) => controller.ListModulesOnBehalfOfAsync(ParentEdgeId, new ListModulesOnBehalfOfData(AuthChainOnParent));
            Func<RegistryController, Task> deleteModuleOnBehalfOfFunc =
                (controller) => controller.DeleteModuleOnBehalfOfAsync(ParentEdgeId, new DeleteModuleOnBehalfOfData(AuthChainOnParent, ModuleId));

            return new List<object[]>
            {
                new object[] { createOrUpdateModuleFunc, $"{ChildEdgeId}" },
                new object[] { getModuleFunc, $"{ChildEdgeId}" },
                new object[] { listModulesFunc, $"{ChildEdgeId}" },
                new object[] { deleteModuleFunc, $"{ChildEdgeId}" },
                new object[] { createOrUpdateModuleOnBehalfOfFunc, $"{ParentEdgeId}" },
                new object[] { getModuleOnBehalfOfFunc, $"{ParentEdgeId}" },
                new object[] { listModulesOnBehalfOfFunc, $"{ParentEdgeId}" },
                new object[] { deleteModuleOnBehalfOfFunc, $"{ParentEdgeId}" },
            };
        }

        (RegistryController, Mock<IRegistryOnBehalfOfApiClient>, Mock<IHttpRequestAuthenticator>, Mock<IDeviceScopeIdentitiesCache>) TestSetup(
            string currentDeviceId,
            IDictionary<string, string> authChains)
        {
            var identitiesCache = new Mock<IDeviceScopeIdentitiesCache>();
            var edgeHub = new Mock<IEdgeHub>();
            edgeHub.Setup(h => h.GetEdgeDeviceId()).Returns(currentDeviceId);
            edgeHub.Setup(h => h.GetDeviceScopeIdentitiesCache()).Returns(identitiesCache.Object);

            foreach (KeyValuePair<string, string> entry in authChains)
            {
                identitiesCache.Setup(c => c.GetAuthChain(It.Is<string>(i => i == entry.Key))).ReturnsAsync(Option.Some(entry.Value));
            }

            var authenticator = new Mock<IHttpRequestAuthenticator>();

            var registryApiClient = new Mock<IRegistryOnBehalfOfApiClient>();
            var controller = new RegistryController(registryApiClient.Object, Task.FromResult(edgeHub.Object), Task.FromResult(authenticator.Object));
            this.SetupControllerContext(controller);

            return (controller, registryApiClient, authenticator, identitiesCache);
        }

        void SetupControllerContext(Controller controller)
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Request.QueryString = new QueryString("?api-version=2017-10-20");

            var httpResponse = new DefaultHttpResponse(httpContext);
            httpResponse.Body = new MemoryStream();
            var controllerContext = new ControllerContext();
            controllerContext.HttpContext = httpContext;
            controller.ControllerContext = controllerContext;
        }

        byte[] GetResponseBodyBytes(Controller controller)
        {
            return (controller.HttpContext.Response.Body as MemoryStream).ToArray();
        }
    }
}
