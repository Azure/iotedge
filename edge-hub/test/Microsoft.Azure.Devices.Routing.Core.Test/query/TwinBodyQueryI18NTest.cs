// Copyright (c) Microsoft. All rights reserved.
/*
namespace Microsoft.Azure.Devices.Routing.Core.Test.Query
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Microsoft.Azure.Devices.Common.Api;
    using Microsoft.Azure.Devices.DeviceManagement.Model;
    using Microsoft.Azure.Devices.Routing.Core.Query;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    public class TwinBodyQueryI18NTest : RoutingUnitTestBase
    {
        public TwinBodyQueryI18NTest()
        {
            Message1 = GenerateDefaultTwinMessage();
        }

        IMessage Message1;
        IMessage GenerateDefaultTwinMessage()
        {
            var twinInfo = new DeviceTwinInfo();
            twinInfo.Tags = new DataObject();
            twinInfo.DesiredProperties = new DeviceTwinProperties();
            twinInfo.ReportedProperties = new DeviceTwinProperties();
            var a1 = new DataObject();
            a1["he"] = "ברוכים הבאים!";
            a1["es"] = "Bienvenido!";

            var a2 = new DataObject();
            a2["he"] = "אני משתמש ב%1";
            a2["es"] = "Estoy usando %1";

            var a3 = new DataObject();
            a3["he"] = "אני משתמש ב%1";
            a3["es"] = "Estoy usando %1";


            twinInfo.Tags["Welcome!"] = a1;
            twinInfo.Tags["I'm using spaces %1"] = a2;
            twinInfo.Tags["I'mnotusingspaces%1"] = a3;

            IMessage twinMessage = new TwinMessage(TwinChangeEventMessageSource.Instance, twinInfo);

            return twinMessage;
        }

        [Theory, Unit]
        [InlineData("$body.tags.Welcome!.he = 'ברוכים הבאים!'")]
        [InlineData("$body.tags.I'mnotusingspaces%1.es = 'Estoy usando %1'")]
        public void BodyQueryI18N_Success(string condition)
        {
            var route = new Route("id", condition, "hub", TwinChangeEventMessageSource.Instance, new HashSet<Endpoint>());
            Func<IMessage, Bool> rule = RouteCompiler.Instance.Compile(route, RouteCompilerFlags.BodyQuery);
            Assert.Equal(rule(Message1), Bool.True);
        }

        [Theory, Unit]
        [InlineData("$body.tags.Welcome!.I'mnotusingspaces%1.es = 'Estoy usando %1'")]
        public void BodyQueryI18N_Failure(string condition)
        {
            var route = new Route("id", condition, "hub", TwinChangeEventMessageSource.Instance, new HashSet<Endpoint>());
            Func<IMessage, Bool> rule = RouteCompiler.Instance.Compile(route, RouteCompilerFlags.BodyQuery);
            Assert.Equal(rule(Message1), Bool.False);
        }

        [Theory, Unit]
        [InlineData("$body.tags.Welcome!['I'm using spaces %1'] = 'ברוכים הבאים!'")]
        public void BodyQueryI18N_RouteCompilation(string condition)
        {
            var route = new Route("id", condition, "hub", TwinChangeEventMessageSource.Instance, new HashSet<Endpoint>());
            Assert.Throws<RouteCompilationException>(() => RouteCompiler.Instance.Compile(route, RouteCompilerFlags.BodyQuery));
        }
    }
}
*/
