// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core.Test.Query
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Routing.Core.MessageSources;
    using Microsoft.Azure.Devices.Routing.Core.Query;
    using Xunit;

    public class TwinChangeIncludesTest : RoutingUnitTestBase
    {
        const string TwinNotificationMessage = "{\"properties\":{\"reported\":{\"WeatherTwin\":{\"Temperature\":50,\"Location\":{\"Street\":\"One Microsoft Way\",\"City\":\"Redmond\",\"State\":\"WA\"}},\"Metadata\":\"metadata\",\"$metadata\":{\"$lastUpdated\":\"2017-02-15T00:42:19.8563501Z\",\"WeatherTwin\":{\"$lastUpdated\":\"2017-02-15T00:42:19.8563501Z\",\"Temperature\":{\"$lastUpdated\":\"2017-02-15T00:42:19.8563501Z\"},\"Location\":{\"$lastUpdated\":\"2017-02-15T00:42:19.8563501Z\",\"Street\":{\"$lastUpdated\":\"2017-02-15T00:42:19.8563501Z\"},\"City\":{\"$lastUpdated\":\"2017-02-15T00:42:19.8563501Z\"},\"State\":{\"$lastUpdated\":\"2017-02-15T00:42:19.8563501Z\"}}},\"Metadata\":{\"$lastUpdated\":\"2017-02-15T00:42:19.8563501Z\"}},\"$version\":4}}}";

        static readonly IMessage Message1 = new Message(
            TwinChangeEventMessageSource.Instance,
            Encoding.UTF8.GetBytes(TwinNotificationMessage),
            new Dictionary<string, string>
            {
                { "key1", "value1" },
                { "key2", "3" },
                { "key3", "VALUE3" },
                { "null_value", null },
                { "$sys4", "value4" }
            },
            new Dictionary<string, string>
            {
                { "sys1", "sysvalue1" },
                { "sys2", "4" },
                { "sys3", "SYSVALUE3" },
                { "sysnull", null },
                { "sys4", "sysvalue4" },
                { SystemProperties.ContentEncoding, "UTF-8" },
                { SystemProperties.ContentType, Constants.SystemPropertyValues.ContentType.Json },
            });

        [Theory]
        [Unit]
        [InlineData("twin_change_includes(properties.reported.WeatherTwin)")]
        [InlineData("TWIN_CHANGE_INCLUDES(properties.reported.WeatherTwin)")] // upper case
        [InlineData("Twin_Change_Includes(properties.reported.WeatherTwin)")] // Mixed case
        [InlineData("twin_CHANGE_includes(properties.reported.WeatherTwin)")] // Mixed case # 2
        [InlineData("twin_change_includes(properties.reported.WeatherTwin.Temperature)")]
        [InlineData("twin_change_includes(properties.reported.WeatherTwin.Location)")]
        [InlineData("twin_change_includes(properties.reported.WeatherTwin.Location.Street)")]
        [InlineData("twin_change_includes(properties.reported.WeatherTwin.Location.City)")]
        [InlineData("twin_change_includes(properties.reported.WeatherTwin.Location.City) AND twin_change_includes(properties.reported.WeatherTwin.Temperature)")]
        [InlineData("twin_change_includes(properties.reported.WeatherTwin.Location.City) AND twin_change_includes(properties.reported.WeatherTwin.Temperature) = true")]
        [InlineData("twin_change_includes(properties.reported.WeatherTwin.Location.City) AND Not(twin_change_includes(properties.reported.WeatherTwin.Temperature123))")]
        public void TestTwinChangeIncludesSuccess(string condition)
        {
            var route = new Route("id", condition, "hub", TwinChangeEventMessageSource.Instance, new HashSet<Endpoint>());
            Func<IMessage, Bool> rule = RouteCompiler.Instance.Compile(route, RouteCompilerFlags.TwinChangeIncludes);
            Assert.Equal(rule(Message1), Bool.True);
        }

        [Theory]
        [Unit]
        [InlineData("twin_change_includes(properties.desired.WeatherTwin)")]
        [InlineData("twin_change_includes(properties.reported.WeatherTwin.Location.City) AND twin_change_includes(properties.reported.WeatherTwin.Temperature123)")]
        public void TestTwinChangeIncludesFailure(string condition)
        {
            var route = new Route("id", condition, "hub", TwinChangeEventMessageSource.Instance, new HashSet<Endpoint>());
            Func<IMessage, Bool> rule = RouteCompiler.Instance.Compile(route, RouteCompilerFlags.TwinChangeIncludes);
            Assert.Equal(rule(Message1), Bool.False);
        }

        [Fact]
        [Unit]
        public void TestTwinChangeIncludes_Debug()
        {
            string condition = "twin_change_includes(properties.reported.WeatherTwin)";

            var route = new Route("id", condition, "hub", TwinChangeEventMessageSource.Instance, new HashSet<Endpoint>());
            Func<IMessage, Bool> rule = RouteCompiler.Instance.Compile(route, RouteCompilerFlags.TwinChangeIncludes);
            Assert.Equal(rule(Message1), Bool.True);
        }

        [Theory]
        [Unit]
        [InlineData("twin_change_includes()")]
        [InlineData("twin_change_includes(      )")]
        [InlineData("twin_change_includes(properties.)")]
        [InlineData("twin_change_includes(123123)")]
        [InlineData("twin_change_includes(properties.abcd123)")]
        [InlineData("twin_change_includes(123abc)")]
        [InlineData("twin_change_includes(properties.desired.twin,,,,,)")]
        [InlineData("twin_change_includes(properties[0])")]
        [InlineData("twin_change_includes(properties.desired.twin[0].test1)")]
        [InlineData("twin_change_includes(properties.desired.[123])")]
        [InlineData("twin_change_includes(properties.desired.WeatherTwin[])")]
        [InlineData("twin_change_includes(properties[])")]
        [InlineData("twin_change_includes(properties[123].)")]
        [InlineData("twin_change_includes(properties[123].props11.)")]
        [InlineData("twin_change_includes(properties[123].props11, abc)")]
        public void TestTwinChangeIncludesCompilationFailure(string condition)
        {
            var route = new Route("id", condition, "hub", TwinChangeEventMessageSource.Instance, new HashSet<Endpoint>());
            Assert.Throws<RouteCompilationException>(() => RouteCompiler.Instance.Compile(route, RouteCompilerFlags.TwinChangeIncludes));
        }

        [Fact]
        [Unit]
        public void TestTwinChangeIncludes_NotSupported_RouteCompilerFlag()
        {
            string condition = "twin_change_includes(properties.reported.WeatherTwin)";
            var route = new Route("id", condition, "hub", TwinChangeEventMessageSource.Instance, new HashSet<Endpoint>());
            Assert.Throws<RouteCompilationException>(() => RouteCompiler.Instance.Compile(route, RouteCompilerFlags.None));
        }

        [Fact]
        [Unit]
        public void TestTwinChangeIncludes_NotSupported_MessageSource()
        {
            string condition = "twin_change_includes(properties.reportedWeatherTwin)";
            var route = new Route("id", condition, "hub", TelemetryMessageSource.Instance, new HashSet<Endpoint>());
            Assert.Throws<RouteCompilationException>(() => RouteCompiler.Instance.Compile(route, RouteCompilerFlags.TwinChangeIncludes));
        }

        [Theory]
        [Unit]
        [InlineData("")]
        [InlineData("random message")]
        public void TestTwinChangeIncludes_ValidateMessage(string messageBody)
        {
            string condition = "twin_change_includes(properties.reported.WeatherTwin)";
            var route = new Route("id", condition, "hub", TwinChangeEventMessageSource.Instance, new HashSet<Endpoint>());
            Func<IMessage, Bool> rule = RouteCompiler.Instance.Compile(route, RouteCompilerFlags.TwinChangeIncludes);

            var invalidMessage = new Message(
                TwinChangeEventMessageSource.Instance,
                Encoding.UTF8.GetBytes(messageBody),
                new Dictionary<string, string>(),
                new Dictionary<string, string>
                {
                    { SystemProperties.ContentEncoding, "UTF-8" },
                    { SystemProperties.ContentType, Constants.SystemPropertyValues.ContentType.Json },
                });

            Assert.Equal(rule(invalidMessage), Bool.Undefined);
        }

        [Fact]
        [Unit]
        public void TestTwinChangeIncludes_InvalidEncoding()
        {
            string condition = "twin_change_includes(properties.reported.WeatherTwin)";
            var route = new Route("id", condition, "hub", TwinChangeEventMessageSource.Instance, new HashSet<Endpoint>());
            Func<IMessage, Bool> rule = RouteCompiler.Instance.Compile(route, RouteCompilerFlags.TwinChangeIncludes);

            var invalidMessage = new Message(
                TwinChangeEventMessageSource.Instance,
                Encoding.Unicode.GetBytes(TwinNotificationMessage),
                new Dictionary<string, string>(),
                new Dictionary<string, string>
                {
                    { SystemProperties.ContentEncoding, "UTF-8" },
                    { SystemProperties.ContentType, Constants.SystemPropertyValues.ContentType.Json },
                });

            Assert.Equal(rule(invalidMessage), Bool.Undefined);
        }
    }
}
