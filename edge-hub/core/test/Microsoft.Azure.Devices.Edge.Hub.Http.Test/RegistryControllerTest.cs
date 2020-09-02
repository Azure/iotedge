// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Http.Test
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Net.Http;
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
        const string ModuleId = "module1";
        const string AuthChain = "childEdge;parentEdge;rootEdge";

        [Fact]
        public async Task CreateOrUpdateModule_Success()
        {
            // Setup
            var authChainMapping = new Dictionary<string, string>()
            {
                { ChildEdgeId, AuthChain }
            };
            (RegistryController controller, Mock<IRegistryApiClient> registryApiClient, Mock<IHttpRequestAuthenticator> authenticator, Mock<IDeviceScopeIdentitiesCache> _) =
                this.TestSetup(ParentEdgeId, authChainMapping);
            CreateOrUpdateModuleOnBehalfOfData actualRequestData = null;
            var module = new Module(ChildEdgeId, ModuleId);
            registryApiClient.Setup(c => c.PutModuleOnBehalfOfAsync(ParentEdgeId, It.IsAny<CreateOrUpdateModuleOnBehalfOfData>()))
                .Callback<string, CreateOrUpdateModuleOnBehalfOfData>((_, data) => actualRequestData = data)
                .Returns(Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonConvert.SerializeObject(module)) } ));
            authenticator.Setup(a => a.AuthenticateAsync(ChildEdgeId, Option.None<string>(), It.IsAny<HttpContext>()))
                .ReturnsAsync(new HttpAuthResult(true, string.Empty));

            // Act
            await controller.CreateOrUpdateModuleAsync(ChildEdgeId, ModuleId, module);

            // Verify
            registryApiClient.Verify(c => c.PutModuleOnBehalfOfAsync(ParentEdgeId, It.IsAny<CreateOrUpdateModuleOnBehalfOfData>()), Times.Once());
            Assert.Equal(AuthChain, actualRequestData.AuthChain);
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
        public async Task CreateOrUpdateModuleOnBehalfOf_Success()
        {
            // Setup
            var authChainMapping = new Dictionary<string, string>()
            {
                { ChildEdgeId, AuthChain }
            };
            (RegistryController controller, Mock<IRegistryApiClient> registryApiClient, Mock<IHttpRequestAuthenticator> authenticator, Mock<IDeviceScopeIdentitiesCache> _) =
                this.TestSetup(ParentEdgeId, authChainMapping);
            CreateOrUpdateModuleOnBehalfOfData actualRequestData = null;
            var module = new Module(ChildEdgeId, ModuleId);
            registryApiClient.Setup(c => c.PutModuleOnBehalfOfAsync(ParentEdgeId, It.IsAny<CreateOrUpdateModuleOnBehalfOfData>()))
                .Callback<string, CreateOrUpdateModuleOnBehalfOfData>((_, data) => actualRequestData = data)
                .Returns(Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonConvert.SerializeObject(module)) }));
            authenticator.Setup(a => a.AuthenticateAsync(ChildEdgeId, Option.Some(Constants.EdgeHubModuleId), It.IsAny<HttpContext>()))
                .ReturnsAsync(new HttpAuthResult(true, string.Empty));

            // Act
            await controller.CreateOrUpdateModuleOnBehalfOfAsync(ChildEdgeId, new CreateOrUpdateModuleOnBehalfOfData { AuthChain = $"{ChildEdgeId};{ParentEdgeId}", Module = module });

            // Verify
            registryApiClient.Verify(c => c.PutModuleOnBehalfOfAsync(ParentEdgeId, It.IsAny<CreateOrUpdateModuleOnBehalfOfData>()), Times.Once());
            Assert.Equal(AuthChain, actualRequestData.AuthChain);
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
                { ChildEdgeId, AuthChain }
            };
            (RegistryController controller, Mock<IRegistryApiClient> registryApiClient, Mock<IHttpRequestAuthenticator> authenticator, Mock<IDeviceScopeIdentitiesCache> _) =
                this.TestSetup(ParentEdgeId, authChainMapping);
            GetModuleOnBehalfOfData actualRequestData = null;
            var module = new Module(ChildEdgeId, ModuleId);
            registryApiClient.Setup(c => c.GetModuleOnBehalfOfAsync(ParentEdgeId, It.IsAny<GetModuleOnBehalfOfData>()))
                .Callback<string, GetModuleOnBehalfOfData>((_, data) => actualRequestData = data)
                .Returns(Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonConvert.SerializeObject(module)) }));
            authenticator.Setup(a => a.AuthenticateAsync(ChildEdgeId, Option.None<string>(), It.IsAny<HttpContext>()))
                .ReturnsAsync(new HttpAuthResult(true, string.Empty));

            // Act
            await controller.GetModuleAsync(ChildEdgeId, ModuleId);

            // Verify
            registryApiClient.Verify(c => c.GetModuleOnBehalfOfAsync(ParentEdgeId, It.IsAny<GetModuleOnBehalfOfData>()), Times.Once());
            Assert.Equal(AuthChain, actualRequestData.AuthChain);
            Assert.Equal(ModuleId, actualRequestData.ModuleId);
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
                { ChildEdgeId, AuthChain }
            };
            (RegistryController controller, Mock<IRegistryApiClient> registryApiClient, Mock<IHttpRequestAuthenticator> authenticator, Mock<IDeviceScopeIdentitiesCache> _) =
                this.TestSetup(ParentEdgeId, authChainMapping);
            GetModuleOnBehalfOfData actualRequestData = null;
            var module = new Module(ChildEdgeId, ModuleId);
            registryApiClient.Setup(c => c.GetModuleOnBehalfOfAsync(ParentEdgeId, It.IsAny<GetModuleOnBehalfOfData>()))
                .Callback<string, GetModuleOnBehalfOfData>((_, data) => actualRequestData = data)
                .Returns(Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonConvert.SerializeObject(module)) }));
            authenticator.Setup(a => a.AuthenticateAsync(ChildEdgeId, Option.Some(Constants.EdgeHubModuleId), It.IsAny<HttpContext>()))
                .ReturnsAsync(new HttpAuthResult(true, string.Empty));

            // Act
            await controller.GetModuleOnBehalfOfAsync(ChildEdgeId, new GetModuleOnBehalfOfData { AuthChain = $"{ChildEdgeId};{ParentEdgeId}", ModuleId = ModuleId });

            // Verify
            registryApiClient.Verify(c => c.GetModuleOnBehalfOfAsync(ParentEdgeId, It.IsAny<GetModuleOnBehalfOfData>()), Times.Once());
            Assert.Equal(AuthChain, actualRequestData.AuthChain);
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
                { ChildEdgeId, AuthChain }
            };
            (RegistryController controller, Mock<IRegistryApiClient> registryApiClient, Mock<IHttpRequestAuthenticator> authenticator, Mock<IDeviceScopeIdentitiesCache> _) =
                this.TestSetup(ParentEdgeId, authChainMapping);
            ListModulesOnBehalfOfData actualRequestData = null;
            var modules = new[] { new Module(ChildEdgeId, "moduleId2"), new Module(ChildEdgeId, "moduleId1") };
            registryApiClient.Setup(c => c.ListModulesOnBehalfOfAsync(ParentEdgeId, It.IsAny<ListModulesOnBehalfOfData>()))
                .Callback<string, ListModulesOnBehalfOfData>((_, data) => actualRequestData = data)
                .Returns(Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonConvert.SerializeObject(modules)) }));
            authenticator.Setup(a => a.AuthenticateAsync(ChildEdgeId, Option.None<string>(), It.IsAny<HttpContext>()))
                .ReturnsAsync(new HttpAuthResult(true, string.Empty));

            // Act
            await controller.ListModulesAsync(ChildEdgeId);

            // Verify
            registryApiClient.Verify(c => c.ListModulesOnBehalfOfAsync(ParentEdgeId, It.IsAny<ListModulesOnBehalfOfData>()), Times.Once());
            Assert.Equal(AuthChain, actualRequestData.AuthChain);
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
        public async Task ListModulesOnBehalfOf_Success()
        {
            // Setup
            var authChainMapping = new Dictionary<string, string>()
            {
                { ChildEdgeId, AuthChain }
            };
            (RegistryController controller, Mock<IRegistryApiClient> registryApiClient, Mock<IHttpRequestAuthenticator> authenticator, Mock<IDeviceScopeIdentitiesCache> _) =
                this.TestSetup(ParentEdgeId, authChainMapping);
            ListModulesOnBehalfOfData actualRequestData = null;
            var modules = new[] { new Module(ChildEdgeId, "moduleId2"), new Module(ChildEdgeId, "moduleId1") };
            registryApiClient.Setup(c => c.ListModulesOnBehalfOfAsync(ParentEdgeId, It.IsAny<ListModulesOnBehalfOfData>()))
                .Callback<string, ListModulesOnBehalfOfData>((_, data) => actualRequestData = data)
                .Returns(Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonConvert.SerializeObject(modules)) }));
            authenticator.Setup(a => a.AuthenticateAsync(ChildEdgeId, Option.Some(Constants.EdgeHubModuleId), It.IsAny<HttpContext>()))
                .ReturnsAsync(new HttpAuthResult(true, string.Empty));

            // Act
            await controller.ListModulesOnBehalfOfAsync(ChildEdgeId, new ListModulesOnBehalfOfData { AuthChain = $"{ChildEdgeId};{ParentEdgeId}" });

            // Verify
            registryApiClient.Verify(c => c.ListModulesOnBehalfOfAsync(ParentEdgeId, It.IsAny<ListModulesOnBehalfOfData>()), Times.Once());
            Assert.Equal(AuthChain, actualRequestData.AuthChain);
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
                { ChildEdgeId, AuthChain }
            };
            (RegistryController controller, Mock<IRegistryApiClient> registryApiClient, Mock<IHttpRequestAuthenticator> authenticator, Mock<IDeviceScopeIdentitiesCache> _) =
                this.TestSetup(ParentEdgeId, authChainMapping);
            DeleteModuleOnBehalfOfData actualRequestData = null;
            var module = new Module(ChildEdgeId, ModuleId);
            registryApiClient.Setup(c => c.DeleteModuleOnBehalfOfAsync(ParentEdgeId, It.IsAny<DeleteModuleOnBehalfOfData>()))
                .Callback<string, DeleteModuleOnBehalfOfData>((_, data) => actualRequestData = data)
                .Returns(Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
            authenticator.Setup(a => a.AuthenticateAsync(ChildEdgeId, Option.None<string>(), It.IsAny<HttpContext>()))
                .ReturnsAsync(new HttpAuthResult(true, string.Empty));

            // Act
            await controller.DeleteModuleAsync(ChildEdgeId, ModuleId);

            // Verify
            registryApiClient.Verify(c => c.DeleteModuleOnBehalfOfAsync(ParentEdgeId, It.IsAny<DeleteModuleOnBehalfOfData>()), Times.Once());
            Assert.Equal(AuthChain, actualRequestData.AuthChain);
            Assert.Equal(ModuleId, actualRequestData.ModuleId);
            Assert.Equal((int)HttpStatusCode.OK, controller.HttpContext.Response.StatusCode);

            byte[] actualResponseBytes = this.GetResponseBodyBytes(controller);
            Assert.Empty(actualResponseBytes);
        }

        [Fact]
        public async Task DeleteModuleOnBehalfOf_Success()
        {
            // Setup
            var authChainMapping = new Dictionary<string, string>()
            {
                { ChildEdgeId, AuthChain }
            };
            (RegistryController controller, Mock<IRegistryApiClient> registryApiClient, Mock<IHttpRequestAuthenticator> authenticator, Mock<IDeviceScopeIdentitiesCache> _) =
                this.TestSetup(ParentEdgeId, authChainMapping);
            DeleteModuleOnBehalfOfData actualRequestData = null;
            registryApiClient.Setup(c => c.DeleteModuleOnBehalfOfAsync(ParentEdgeId, It.IsAny<DeleteModuleOnBehalfOfData>()))
                .Callback<string, DeleteModuleOnBehalfOfData>((_, data) => actualRequestData = data)
                .Returns(Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
            authenticator.Setup(a => a.AuthenticateAsync(ChildEdgeId, Option.Some(Constants.EdgeHubModuleId), It.IsAny<HttpContext>()))
                .ReturnsAsync(new HttpAuthResult(true, string.Empty));

            // Act
            await controller.DeleteModuleOnBehalfOfAsync(ChildEdgeId, new DeleteModuleOnBehalfOfData { AuthChain = ChildEdgeId, ModuleId = ModuleId });

            // Verify
            registryApiClient.Verify(c => c.DeleteModuleOnBehalfOfAsync(ParentEdgeId, It.IsAny<DeleteModuleOnBehalfOfData>()), Times.Once());
            Assert.Equal(AuthChain, actualRequestData.AuthChain);
            Assert.Equal(ModuleId, actualRequestData.ModuleId);
            Assert.Equal((int)HttpStatusCode.OK, controller.HttpContext.Response.StatusCode);

            byte[] actualResponseBytes = this.GetResponseBodyBytes(controller);
            Assert.Empty(actualResponseBytes);
        }

        [Theory]
        [MemberData(nameof(GetControllerMethodDelegates))]
        public async Task AuthenticateFailed(Func<RegistryController, Task> funcDelegate)
        {
            // Setup
            var authChainMapping = new Dictionary<string, string>()
            {
                { ChildEdgeId, AuthChain }
            };
            (RegistryController controller, Mock<IRegistryApiClient> _, Mock<IHttpRequestAuthenticator> _, Mock<IDeviceScopeIdentitiesCache> _) =
                this.TestSetup(ParentEdgeId, authChainMapping);

            // Act
            await funcDelegate(controller);

            // Verify
            Assert.Equal((int)HttpStatusCode.Unauthorized, controller.HttpContext.Response.StatusCode);
        }

        [Theory]
        [MemberData(nameof(GetControllerMethodDelegates))]
        public async Task AuthorizeFailed(Func<RegistryController, Task> funcDelegate)
        {
            // Setup
            (RegistryController controller, Mock<IRegistryApiClient> _, Mock<IHttpRequestAuthenticator> authenticator, Mock<IDeviceScopeIdentitiesCache> identitiesCache) =
                this.TestSetup(ParentEdgeId, new Dictionary<string, string>());
            authenticator.Setup(a => a.AuthenticateAsync(ChildEdgeId, It.IsAny<Option<string>>(), It.IsAny<HttpContext>()))
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
                (controller) => controller.CreateOrUpdateModuleAsync(ChildEdgeId, ModuleId, module);
            Func<RegistryController, Task> createOrUpdateModuleOnBehalfOfFunc =
                (controller) => controller.CreateOrUpdateModuleOnBehalfOfAsync(ChildEdgeId, new CreateOrUpdateModuleOnBehalfOfData { AuthChain = $"{ChildEdgeId};{ParentEdgeId}", Module = module });
            Func<RegistryController, Task> getModuleFunc =
                (controller) => controller.GetModuleAsync(ChildEdgeId, ModuleId);
            Func<RegistryController, Task> getModuleOnBehalfOfFunc =
                (controller) => controller.GetModuleOnBehalfOfAsync(ChildEdgeId, new GetModuleOnBehalfOfData { AuthChain = $"{ChildEdgeId};{ParentEdgeId}", ModuleId = ModuleId });
            Func<RegistryController, Task> listModulesFunc =
                (controller) => controller.ListModulesAsync(ChildEdgeId);
            Func<RegistryController, Task> listModulesOnBehalfOfFunc =
                (controller) => controller.ListModulesOnBehalfOfAsync(ChildEdgeId, new ListModulesOnBehalfOfData { AuthChain = $"{ChildEdgeId};{ParentEdgeId}" });
            Func<RegistryController, Task> deleteModuleFunc =
                (controller) => controller.DeleteModuleAsync(ChildEdgeId, ModuleId);
            Func<RegistryController, Task> deleteModuleOnBehalfOfFunc =
                (controller) => controller.DeleteModuleOnBehalfOfAsync(ChildEdgeId, new DeleteModuleOnBehalfOfData { AuthChain = ChildEdgeId, ModuleId = ModuleId });

            return new List<object[]>
            {
                new object[] { createOrUpdateModuleFunc },
                new object[] { createOrUpdateModuleOnBehalfOfFunc },
                new object[] { getModuleFunc },
                new object[] { getModuleOnBehalfOfFunc },
                new object[] { listModulesFunc },
                new object[] { listModulesOnBehalfOfFunc },
                new object[] { deleteModuleFunc },
                new object[] { deleteModuleOnBehalfOfFunc },
            };
        }

        (RegistryController, Mock<IRegistryApiClient>, Mock<IHttpRequestAuthenticator>, Mock<IDeviceScopeIdentitiesCache>) TestSetup(
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

            var registryApiClient = new Mock<IRegistryApiClient>();
            var controller = new RegistryController(registryApiClient.Object, Task.FromResult(edgeHub.Object), Task.FromResult(authenticator.Object));
            this.SetupControllerContext(controller);

            return (controller, registryApiClient, authenticator, identitiesCache);
        }

        void SetupControllerContext(Controller controller)
        {
            var httpContext = new DefaultHttpContext();
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
