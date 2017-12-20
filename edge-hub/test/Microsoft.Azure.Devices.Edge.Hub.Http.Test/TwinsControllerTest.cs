// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Http.Test
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.Abstractions;
    using Microsoft.AspNetCore.Mvc.Filters;
    using Microsoft.AspNetCore.Routing;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Hub.Http;
    using Microsoft.Azure.Devices.Edge.Hub.Http.Controllers;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Newtonsoft.Json.Linq;
    using Xunit;

    [Unit]
    public class TwinsControllerTest
    {
        [Fact]
        public async Task TestInvokeMethod()
        {
            var identity = Mock.Of<IIdentity>(i => i.Id == "edgedevice/module1");
            ActionExecutingContext actionExecutingContext = this.GetActionExecutingContextMock(identity);

            var directMethodResponse = new DirectMethodResponse(Guid.NewGuid().ToString(), new byte[0], 200);
            var edgeHub = new Mock<IEdgeHub>();
            edgeHub.Setup(e => e.InvokeMethodAsync(It.Is<string>(i => i == identity.Id), It.IsAny<DirectMethodRequest>()))
                .ReturnsAsync(directMethodResponse);

            var validator = new Mock<IValidator<MethodRequest>>();
            validator.Setup(v => v.Validate(It.IsAny<MethodRequest>()));

            var testController = new TwinsController(Task.FromResult(edgeHub.Object), validator.Object);
            testController.OnActionExecuting(actionExecutingContext);

            string toDeviceId = "device1";
            string command = "showdown";
            string payload = "{ \"prop1\" : \"value1\" }";

            var methodRequest = new MethodRequest { MethodName = command, Payload = new JRaw(payload) };
            IActionResult actionResult = await testController.InvokeDeviceMethodAsync(toDeviceId, methodRequest);

            Assert.NotNull(actionResult);
            var jsonResult = actionResult as JsonResult;
            Assert.NotNull(jsonResult);
            var methodResult = jsonResult.Value as MethodResult;
            Assert.NotNull(methodResult);
            Assert.Equal(200, methodResult.Status);
            Assert.Equal(string.Empty, methodResult.Payload);
        }

        [Fact]
        public async Task TestInvokeMethodOnModule()
        {
            var identity = Mock.Of<IIdentity>(i => i.Id == "edgedevice/module1");
            ActionExecutingContext actionExecutingContext = this.GetActionExecutingContextMock(identity);

            var directMethodResponse = new DirectMethodResponse(Guid.NewGuid().ToString(), new byte[0], 200);
            var edgeHub = new Mock<IEdgeHub>();
            edgeHub.Setup(e => e.InvokeMethodAsync(It.Is<string>(i => i == identity.Id), It.IsAny<DirectMethodRequest>()))
                .ReturnsAsync(directMethodResponse);

            var validator = new Mock<IValidator<Http.MethodRequest>>();
            validator.Setup(v => v.Validate(It.IsAny<Http.MethodRequest>()));

            var testController = new TwinsController(Task.FromResult(edgeHub.Object), validator.Object);
            testController.OnActionExecuting(actionExecutingContext);

            string toDeviceId = "edgedevice";
            string toModuleId = "module2";
            string command = "showdown";
            string payload = "{ \"prop1\" : \"value1\" }";

            var methodRequest = new Http.MethodRequest { MethodName = command, Payload = new JRaw(payload) };
            IActionResult actionResult = await testController.InvokeModuleMethodAsync(WebUtility.UrlEncode(toDeviceId), WebUtility.UrlEncode(toModuleId), methodRequest);

            Assert.NotNull(actionResult);
            var jsonResult = actionResult as JsonResult;
            Assert.NotNull(jsonResult);
            var methodResult = jsonResult.Value as MethodResult;
            Assert.NotNull(methodResult);
            Assert.Equal(200, methodResult.Status);
            Assert.Equal(string.Empty, methodResult.Payload);
        }

        ActionExecutingContext GetActionExecutingContextMock(IIdentity identity)
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
    }
}
