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
    using Microsoft.AspNetCore.Mvc.Abstractions;
    using Microsoft.AspNetCore.Mvc.Filters;
    using Microsoft.AspNetCore.Routing;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Hub.Http.Controllers;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using Xunit;

    [Unit]
    public class TwinsControllerTest
    {
        static readonly string actorDevice = "actorEdgeDevice";
        static readonly string actorId = $"{actorDevice}/actorModule";

        [Fact]
        public async Task InvokeDeviceMethodNoPayloadReturnsOk()
        {
            string toDeviceId = "device1";
            var sut = SetupControllerToRespond(toDeviceId, Option.None<string>(), 200, new byte[0]);
            sut.HttpContext.Request.Headers.Add(Constants.ServiceApiIdHeaderKey, actorId);

            string command = "showdown";
            string payload = "{ \"prop1\" : \"value1\" }";

            var methodRequest = new MethodRequest(command, new JRaw(payload));
            await sut.InvokeDeviceMethodAsync(toDeviceId, methodRequest);

            var responseBytes = GetResponseBodyBytes(sut);
            var responseJsonString = Encoding.UTF8.GetString(responseBytes);

            Assert.Equal(200, sut.HttpContext.Response.StatusCode);
            Assert.Equal(@"{""status"":200,""payload"":null}", responseJsonString);
        }

        [Fact]
        public async Task InvokeModuleMethodNoPayloadReturnsOk()
        {
            string toDeviceId = "edgedevice";
            string toModuleId = "module2";
            var sut = SetupControllerToRespond(toDeviceId, Option.Some(toModuleId), 200, new byte[0]);
            sut.HttpContext.Request.Headers.Add(Constants.ServiceApiIdHeaderKey, actorId);

            string command = "showdown";
            string payload = "{ \"prop1\" : \"value1\" }";

            var methodRequest = new MethodRequest(command, new JRaw(payload));
            await sut.InvokeModuleMethodAsync(toDeviceId, toModuleId, methodRequest);

            var responseBytes = GetResponseBodyBytes(sut);
            var responseJsonString = Encoding.UTF8.GetString(responseBytes);

            Assert.Equal(200, sut.HttpContext.Response.StatusCode);
            Assert.Equal(@"{""status"":200,""payload"":null}", responseJsonString);
        }

        [Fact]
        public async Task InvokeDeviceMethodExpectPayloadReturnsOk()
        {
            string toDeviceId = "device1";
            var reponsePayloadJson = "{ \"resp1\" : \"respvalue1\" }";
            var responsePayloadBytes = Encoding.UTF8.GetBytes(reponsePayloadJson);

            var sut = SetupControllerToRespond(toDeviceId, Option.None<string>(), 200, responsePayloadBytes);
            sut.HttpContext.Request.Headers.Add(Constants.ServiceApiIdHeaderKey, actorId);

            string command = "showdown";
            string cmdPayload = "{ \"prop1\" : \"value1\" }";

            var methodRequest = new MethodRequest(command, new JRaw(cmdPayload));
            await sut.InvokeDeviceMethodAsync(toDeviceId, methodRequest);

            var responseBytes = GetResponseBodyBytes(sut);
            var responseJsonString = Encoding.UTF8.GetString(responseBytes);

            Assert.Equal(200, sut.HttpContext.Response.StatusCode);
            Assert.Equal(@"{""status"":200,""payload"":" + reponsePayloadJson + "}", responseJsonString);
        }

        [Fact]
        public async Task InvokeModuleMethodExpectPayloadReturnsOk()
        {
            string toDeviceId = "edgedevice";
            string toModuleId = "module2";
            var reponsePayloadJson = "{ \"resp1\" : \"respvalue1\" }";
            var responsePayloadBytes = Encoding.UTF8.GetBytes(reponsePayloadJson);

            var sut = SetupControllerToRespond(toDeviceId, Option.Some(toModuleId), 200, responsePayloadBytes);
            sut.HttpContext.Request.Headers.Add(Constants.ServiceApiIdHeaderKey, actorId);

            string command = "showdown";
            string payload = "{ \"prop1\" : \"value1\" }";

            var methodRequest = new MethodRequest(command, new JRaw(payload));
            await sut.InvokeModuleMethodAsync(toDeviceId, toModuleId, methodRequest);

            var responseBytes = GetResponseBodyBytes(sut);
            var responseJsonString = Encoding.UTF8.GetString(responseBytes);

            Assert.Equal(200, sut.HttpContext.Response.StatusCode);
            Assert.Equal(@"{""status"":200,""payload"":" + reponsePayloadJson + "}", responseJsonString);
        }

        [Fact]
        public async Task NoActorHeaderReturnsError()
        {
            string toDeviceId = "device1";
            var sut = SetupControllerToRespond(toDeviceId, Option.None<string>(), 200, new byte[0]);

            string command = "showdown";
            string payload = "{ \"prop1\" : \"value1\" }";

            var methodRequest = new MethodRequest(command, new JRaw(payload));
            await sut.InvokeDeviceMethodAsync(toDeviceId, methodRequest);

            Assert.Equal((int)HttpStatusCode.BadRequest, sut.HttpContext.Response.StatusCode);
        }

        [Fact]
        public async Task ActorNotModuleReturnsError()
        {
            string toDeviceId = "device1";
            var sut = SetupControllerToRespond(toDeviceId, Option.None<string>(), 200, new byte[0]);
            sut.HttpContext.Request.Headers.Add(Constants.ServiceApiIdHeaderKey, $"{actorDevice}");

            string command = "showdown";
            string payload = "{ \"prop1\" : \"value1\" }";

            var methodRequest = new MethodRequest(command, new JRaw(payload));
            await sut.InvokeDeviceMethodAsync(toDeviceId, methodRequest);

            Assert.Equal((int)HttpStatusCode.BadRequest, sut.HttpContext.Response.StatusCode);
        }

        [Fact]
        public async Task ActorTargetDeviceMismatchReturnsError()
        {
            string toDeviceId = "device1";
            var sut = SetupControllerToRespond(toDeviceId, Option.None<string>(), 200, new byte[0]);
            sut.HttpContext.Request.Headers.Add(Constants.ServiceApiIdHeaderKey, $"differentEdge/module1");

            string command = "showdown";
            string payload = "{ \"prop1\" : \"value1\" }";

            var methodRequest = new MethodRequest(command, new JRaw(payload));
            await sut.InvokeDeviceMethodAsync(toDeviceId, methodRequest);

            Assert.Equal((int)HttpStatusCode.Unauthorized, sut.HttpContext.Response.StatusCode);
        }

        [Fact]
        public async Task InvokeDeviceMethodThrowingReturnsError()
        {
            string toDeviceId = "device1";
            var sut = SetupControllerToThrow(toDeviceId, Option.None<string>(), HttpStatusCode.GatewayTimeout, new EdgeHubTimeoutException("EdgeHub timed out"));
            sut.HttpContext.Request.Headers.Add(Constants.ServiceApiIdHeaderKey, actorId);

            string command = "showdown";
            string cmdPayload = "{ \"prop1\" : \"value1\" }";

            var methodRequest = new MethodRequest(command, new JRaw(cmdPayload));
            await sut.InvokeDeviceMethodAsync(toDeviceId, methodRequest);

            var responseBytes = GetResponseBodyBytes(sut);
            var responseJsonString = Encoding.UTF8.GetString(responseBytes);

            Assert.Equal(504, sut.HttpContext.Response.StatusCode);
            Assert.Equal(@"{""message"":""EdgeHub timed out""}", responseJsonString);
        }

        [Fact]
        public async Task InvokeModuleMethodMultibyteContentSetsContentLength()
        {
            string toDeviceId = "edgedevice";
            string toModuleId = "module2";
            var reponsePayloadJson = "{ \"resp1\" : \"王明是中国人。\" }"; // supposed to be "Wang Ming is Chinese."
            var responsePayloadBytes = Encoding.UTF8.GetBytes(reponsePayloadJson);

            // make sure that this is a good test and the string length and the encoding length is not the same
            Assert.True(reponsePayloadJson.Length != responsePayloadBytes.Length);

            var sut = SetupControllerToRespond(toDeviceId, Option.Some(toModuleId), 200, responsePayloadBytes);
            sut.HttpContext.Request.Headers.Add(Constants.ServiceApiIdHeaderKey, actorId);

            string command = "showdown";
            string payload = "{ \"prop1\" : \"value1\" }";

            var methodRequest = new MethodRequest(command, new JRaw(payload));
            await sut.InvokeModuleMethodAsync(toDeviceId, toModuleId, methodRequest);

            var responseBytes = GetResponseBodyBytes(sut);
            var responseJsonString = Encoding.UTF8.GetString(responseBytes);

            Assert.Equal(200, sut.HttpContext.Response.StatusCode);
            Assert.Equal(@"{""status"":200,""payload"":" + reponsePayloadJson + "}", responseJsonString);
            Assert.True(sut.HttpContext.Response.ContentLength == responseBytes.Length);
        }

        [Unit]
        [Theory]
        [MemberData(nameof(GetRawJsonData))]
        public void GetRawJsonTest(byte[] input, JRaw expectedOutput)
        {
            // Act
            JRaw output = TwinsController.GetRawJson(input);

            // Assert
            Assert.Equal(expectedOutput, output);
        }

        public static IEnumerable<object[]> GetRawJsonData()
        {
            yield return new object[] { null, null };

            yield return new object[] { new byte[0], null };

            object obj = new
            {
                prop1 = "foo",
                prop2 = new
                {
                    prop3 = 100
                }
            };
            string json = JsonConvert.SerializeObject(obj);
            yield return new object[] { Encoding.UTF8.GetBytes(json), new JRaw(json) };
        }

        private static ActionExecutingContext GetActionExecutingContextMock(IIdentity identity)
        {
            var items = new Dictionary<object, object>
            {
                { HttpConstants.IdentityKey, identity }
            };

            var httpContext = Mock.Of<HttpContext>(c => c.Items == items);
            var actionContext = new ActionContext(httpContext, Mock.Of<RouteData>(), Mock.Of<ActionDescriptor>());
            var actionExecutingContext = new ActionExecutingContext(actionContext, Mock.Of<IList<IFilterMetadata>>(), Mock.Of<IDictionary<string, object>>(), new object());
            return actionExecutingContext;
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

        private static Task<IEdgeHub> CreateEdgeHubGetter(string targetDevice, Option<string> targetModule, DirectMethodResponse directMethodResponse)
        {
            string targetId = targetDevice + targetModule.Match(module => $"/{module}", () => string.Empty);

            var edgeHub = new Mock<IEdgeHub>();
            edgeHub.Setup(e => e.InvokeMethodAsync(It.Is<string>(i => i == actorId), It.Is<DirectMethodRequest>(r => r.Id == targetId)))
                .ReturnsAsync(directMethodResponse);
            edgeHub.Setup(e => e.GetEdgeDeviceId())
                .Returns(actorDevice);

            return Task.FromResult(edgeHub.Object);
        }

        private static IValidator<MethodRequest> CreateLetThroughValidator()
        {
            var validator = new Mock<IValidator<MethodRequest>>();
            validator.Setup(v => v.Validate(It.IsAny<MethodRequest>()));

            return validator.Object;
        }

        private static TwinsController SetupControllerToRespond(string targetDevice, Option<string> targetModule, int responseStatusCode, byte[] responsePayload)
        {
            return SetupController(
                      CreateEdgeHubGetter(
                          targetDevice,
                          targetModule,
                          new DirectMethodResponse(Guid.NewGuid().ToString(), responsePayload, responseStatusCode)));
        }

        private static TwinsController SetupControllerToThrow(string targetDevice, Option<string> targetModule, HttpStatusCode responseStatusCode, Exception exception)
        {
            return SetupController(
                      CreateEdgeHubGetter(
                          targetDevice,
                          targetModule,
                          new DirectMethodResponse(exception, responseStatusCode)));
        }

        private static TwinsController SetupController(Task<IEdgeHub> edgeHubGetter)
        {
            var authenticator = new Mock<IHttpRequestAuthenticator>();
            authenticator.Setup(a => a.AuthenticateAsync(It.IsAny<string>(), It.IsAny<Option<string>>(), It.IsAny<Option<string>>(), It.IsAny<HttpContext>()))
                .ReturnsAsync(new HttpAuthResult(true, string.Empty));

            var controller = new TwinsController(edgeHubGetter, Task.FromResult(authenticator.Object), CreateLetThroughValidator());
            SetupControllerContext(controller);

            return controller;
        }

        private static byte[] GetResponseBodyBytes(Controller controller)
        {
            return (controller.HttpContext.Response.Body as MemoryStream).ToArray();
        }
    }
}
