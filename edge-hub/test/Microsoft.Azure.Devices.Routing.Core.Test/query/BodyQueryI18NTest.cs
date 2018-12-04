// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Routing.Core.Test.Query
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Microsoft.Azure.Devices.Routing.Core.Query;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Routing.Core.MessageSources;
    using Xunit;

    public class BodyQueryI18NTest : RoutingUnitTestBase
    {
        const string MessageBody =
            @"{
        ""Welcome!"": {
                ""he"": ""ברוכים הבאים!"",
                ""es"": ""Bienvenido!""
        },
        ""I'm using spaces %1"": {
                ""he"": ""אני משתמש ב%1"",
                ""es"": ""Estoy usando %1""
        },
        ""I'mnotusingspaces%1"": {
                ""he"": ""אני משתמש ב%1"",
                ""es"": ""Estoy usando %1""
        }}";

        static readonly IMessage Message1 = new Message(TelemetryMessageSource.Instance,
            Encoding.UTF8.GetBytes(MessageBody),
            new Dictionary<string, string>(),
            new Dictionary<string, string>()
            {
                {  SystemProperties.ContentEncoding, "UTF-8" },
                {  SystemProperties.ContentType, Constants.SystemPropertyValues.ContentType.Json },
            });

        [Theory, Unit]
        [InlineData("$body.Welcome!.he = 'ברוכים הבאים!'")]
        [InlineData("$body.I'mnotusingspaces%1.es = 'Estoy usando %1'")]
        public void BodyQueryI18N_Success(string condition)
        {
            var route = new Route("id", condition, "hub", TelemetryMessageSource.Instance, new HashSet<Endpoint>());
            Func<IMessage, Bool> rule = RouteCompiler.Instance.Compile(route, RouteCompilerFlags.BodyQuery);
            Assert.Equal(rule(Message1), Bool.True);
        }

        [Theory, Unit]
        [InlineData("$body.Welcome!.I'mnotusingspaces%1.es = 'Estoy usando %1'")]
        public void BodyQueryI18N_Failure(string condition)
        {
            var route = new Route("id", condition, "hub", TelemetryMessageSource.Instance, new HashSet<Endpoint>());
            Func<IMessage, Bool> rule = RouteCompiler.Instance.Compile(route, RouteCompilerFlags.BodyQuery);
            Assert.Equal(rule(Message1), Bool.False);
        }

        [Theory, Unit]
        [InlineData("$body.Welcome!['I'm using spaces %1'] = 'ברוכים הבאים!'")]
        public void BodyQueryI18N_RouteCompilation(string condition)
        {
            var route = new Route("id", condition, "hub", TelemetryMessageSource.Instance, new HashSet<Endpoint>());
            Assert.Throws<RouteCompilationException>(() => RouteCompiler.Instance.Compile(route, RouteCompilerFlags.BodyQuery));
        }
    }
}
