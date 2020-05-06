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
    using Microsoft.AspNetCore.Http.Internal;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.Abstractions;
    using Microsoft.AspNetCore.Mvc.Filters;
    using Microsoft.AspNetCore.Routing;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Hub.Http.Controllers;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using Xunit;

    [Unit]
    public class TwinsControllerTest
    {
        static readonly string defaultModuleIdentity = "edgedevice/module1";

        [Fact]
        public async Task InvokeDeviceMethodNoPayloadReturnsOk()
        {
            var sut = SetupControllerToRespond(200, new byte[0]);

            string toDeviceId = "device1";
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
            var sut = SetupControllerToRespond(200, new byte[0]);

            string toDeviceId = "edgedevice";
            string toModuleId = "module2";
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
            var reponsePayloadJson = "{ \"resp1\" : \"respvalue1\" }";
            var responsePayloadBytes = Encoding.UTF8.GetBytes(reponsePayloadJson);

            var sut = SetupControllerToRespond(200, responsePayloadBytes);

            string toDeviceId = "device1";
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
            var reponsePayloadJson = "{ \"resp1\" : \"respvalue1\" }";
            var responsePayloadBytes = Encoding.UTF8.GetBytes(reponsePayloadJson);

            var sut = SetupControllerToRespond(200, responsePayloadBytes);

            string toDeviceId = "edgedevice";
            string toModuleId = "module2";
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
        public async Task InvokeDeviceMethodThrowingReturnsError()
        {
            var sut = SetupControllerToThrow(HttpStatusCode.GatewayTimeout, new EdgeHubTimeoutException("EdgeHub timed out"));

            string toDeviceId = "device1";
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
            var reponsePayloadJson = "{ \"resp1\" : \"王明是中国人。\" }"; // supposed to be "Wang Ming is Chinese."
            var responsePayloadBytes = Encoding.UTF8.GetBytes(reponsePayloadJson);

            // make sure that this is a good test and the string length and the encoding length is not the same
            Assert.True(reponsePayloadJson.Length != responsePayloadBytes.Length);

            var sut = SetupControllerToRespond(200, responsePayloadBytes);

            string toDeviceId = "edgedevice";
            string toModuleId = "module2";
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
            var controllerContex = new ControllerContext();
            controllerContex.HttpContext = httpContext;

            controller.ControllerContext = controllerContex;
        }

        private static Task<IEdgeHub> CreateEdgeHubGetter(string id, DirectMethodResponse directMethodResponse)
        {
            var edgeHub = new Mock<IEdgeHub>();
            edgeHub.Setup(e => e.InvokeMethodAsync(It.Is<string>(i => i == id), It.IsAny<DirectMethodRequest>()))
                .ReturnsAsync(directMethodResponse);

            return Task.FromResult(edgeHub.Object);
        }

        private static IValidator<MethodRequest> CreateLetThroughValidator()
        {
            var validator = new Mock<IValidator<MethodRequest>>();
            validator.Setup(v => v.Validate(It.IsAny<MethodRequest>()));

            return validator.Object;
        }

        private static TwinsController SetupControllerToRespond(int responseStatusCode, byte[] responsePayload)
        {
            return SetupController(
                      defaultModuleIdentity,
                      CreateEdgeHubGetter(
                          defaultModuleIdentity,
                          new DirectMethodResponse(Guid.NewGuid().ToString(), responsePayload, responseStatusCode)));
        }

        private static TwinsController SetupControllerToThrow(HttpStatusCode responseStatusCode, Exception exception)
        {
            return SetupController(
                      defaultModuleIdentity,
                      CreateEdgeHubGetter(
                          defaultModuleIdentity,
                          new DirectMethodResponse(exception, responseStatusCode)));
        }

        private static TwinsController SetupController(string id, Task<IEdgeHub> edgeHubGetter)
        {
            var identity = Mock.Of<IIdentity>(i => i.Id == id);
            ActionExecutingContext actionExecutingContext = GetActionExecutingContextMock(identity);

            var controller = new TwinsController(edgeHubGetter, CreateLetThroughValidator());

            controller.OnActionExecuting(actionExecutingContext);
            SetupControllerContext(controller);

            return controller;
        }

        private static byte[] GetResponseBodyBytes(Controller controller)
        {
            return (controller.HttpContext.Response.Body as MemoryStream).ToArray();
        }
    }
}
