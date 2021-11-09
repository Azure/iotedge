// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Http.Test
{
    using System;
    using System.IO;
    using System.Net;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Billing;
    using Microsoft.Azure.Devices.Edge.Hub.Http;
    using Microsoft.Azure.Devices.Edge.Hub.Http.Controllers;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Newtonsoft.Json;
    using Xunit;

    [Unit]
    public class BillingControllerTest
    {
        [Fact]
        public async Task GetPurchase()
        {
            string childEdgeId = "edge2";
            string moduleId = "module1";

            PurchaseContent purchaseContent = new PurchaseContent()
            {
                PublisherId = "id",
                OfferId = "offer1",
                PlanId = "plan1"
            };
            var controller = MakeController(childEdgeId, moduleId, purchaseContent);

            // Act
            await controller.GetPurchaseAsync(childEdgeId, moduleId);

            var responseActualBytes = GetResponseBodyBytes(controller);
            var responseActualJson = Encoding.UTF8.GetString(responseActualBytes);
            var responsePurchase = JsonConvert.DeserializeObject<PurchaseResultSuccess>(responseActualJson);

            Assert.Equal((int)HttpStatusCode.OK, controller.HttpContext.Response.StatusCode);
            Assert.Equal(purchaseContent.PublisherId, responsePurchase.PublisherId);
            Assert.Equal(purchaseContent.OfferId, responsePurchase.OfferId);
            Assert.Equal(purchaseContent.PlanId, responsePurchase.PlanId);
            Assert.Equal(PurchaseStatus.Complete, responsePurchase.PurchaseStatus);
        }

        private static BillingController MakeController(string targetEdgeId, string moduleId, PurchaseContent purchaseContent)
        {
            var identitiesCache = new Mock<IDeviceScopeIdentitiesCache>();
            var edgeHub = new Mock<IEdgeHub>();
            edgeHub.Setup(e => e.GetDeviceScopeIdentitiesCache())
                .Returns(identitiesCache.Object);

            identitiesCache.Setup(c => c.GetPurchaseAsync(It.Is<string>(id => id == targetEdgeId), It.Is<string>(id => id == moduleId)))
                .ReturnsAsync(new SynchedPurchase(DateTime.UtcNow, Option.Some(purchaseContent)));

            var authenticator = new Mock<IHttpRequestAuthenticator>();
            authenticator.Setup(a => a.AuthenticateAsync(It.IsAny<string>(), It.IsAny<Option<string>>(), It.IsAny<Option<string>>(), It.IsAny<HttpContext>()))
                .ReturnsAsync(new HttpAuthResult(true, string.Empty));

            var controller = new BillingController(Task.FromResult(edgeHub.Object), Task.FromResult(authenticator.Object));
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
