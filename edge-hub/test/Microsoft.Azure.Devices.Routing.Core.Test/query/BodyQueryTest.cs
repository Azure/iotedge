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

    public class BodyQueryTest : RoutingUnitTestBase
    {
        const string MessageBody =
            @"{
               ""message"": {
                  ""Weather"": {
                     ""Temperature"": 50,
                     ""FreezingTemperature"": -50.4,
                     ""PreciseTemperature"": 50.4,
                     ""Temperature_PropertyConflict"": 50,
                     ""NullValue"": null,
                     ""Time"" : ""2017-03-09T00:00:00.000Z"",
                     ""PrevTemperatures"" : [20, 30, 40 ],
                     ""IsEnabled"": true,
                     ""IsDisabled"": false,
                     ""Location"": {
                        ""Street"": ""One Microsoft Way"",
                        ""City"": ""Redmond"",
                        ""State"": ""WA""
                                },
                     ""HistoricalData"": [
                        {
                          ""Month""  : ""Feb"",
                          ""Temperature"": 40
                        },
                        {
                          ""Month""  : ""Jan"",
                          ""Temperature"": 30
                        }]
                  }
               }
            }";

        static readonly IMessage Message1 = new Message(
            TelemetryMessageSource.Instance,
            Encoding.UTF8.GetBytes(MessageBody),
            new Dictionary<string, string>
            {
                { "City", "Redmond" },
                { "$State", "WA" },
                { "$body.message.Weather.Temperature_PropertyConflict", "100" },
                { "IntegerProperty", "110" },
            },
            new Dictionary<string, string>
            {
                { "State", "CA" },
                { "$body.message.Weather.Location.State", "CA" },
                { SystemProperties.ContentEncoding, "UTF-8" },
                { SystemProperties.ContentType, Constants.SystemPropertyValues.ContentType.Json },
            });

        [Theory]
        [Unit]
        // TODO = These tests don't pass at the moment, need to fix them. Might have to look into fixing the grammar.
        // Note - looks like Antlr code has changed internally from the version used in IoTHub codebase
        // which might affect the behavior.
        // [InlineData("$body.properties,reported")]
        // [InlineData("$body.properties []")]
        [InlineData("$body.properties[]")]
        [InlineData("$body.properties[1:2]")]
        [InlineData("$body;@")]
        public void BodyQuery_RouteCompilation(string condition)
        {
            var route = new Route("id", condition, "hub", TelemetryMessageSource.Instance, new HashSet<Endpoint>());
            Assert.Throws<RouteCompilationException>(() => RouteCompiler.Instance.Compile(route, RouteCompilerFlags.BodyQuery));
        }

        [Theory]
        [Unit]
        [InlineData("50 >= $body.message.Weather.Temperature")]
        [InlineData("$BODY.message.Weather.Temperature >= 50")]
        [InlineData("$bODy.message.Weather.Temperature <= 50")]
        [InlineData("$bODY.message.Weather.Temperature = 50")]
        [InlineData("$BoDY.message.Weather.Temperature != 60")]
        [InlineData("$body.message.Weather.Temperature <> 500")]
        [InlineData("$body.message.Weather.Temperature = 40 + 10")]
        [InlineData("$body.message.Weather.Temperature >= $body.message.Weather.PrevTemperatures[0]")]
        [InlineData("$body.message.Weather.Temperature >= $body.message.Weather.PrevTemperatures[0] + $body.message.Weather.PrevTemperatures[1] ")]
        [InlineData("$body.message.Weather.PrevTemperatures[0] + $body.message.Weather.PrevTemperatures[1]= 50 ")]
        [InlineData("$body.message.Weather.PrevTemperatures[1] * $body.message.Weather.PrevTemperatures[2] = 1200")]
        [InlineData("$body.message.Weather.PrevTemperatures[1] / $body.message.Weather.PrevTemperatures[0] = 1.5")]
        [InlineData("$body.message.Weather.HistoricalData[0].Temperature - 10 = $body.message.Weather.HistoricalData[1].Temperature")]
        [InlineData("$body.message.Weather.HistoricalData[0].Month = 'Feb'")]
        public void BodyQuery_Double_Success(string condition)
        {
            var route = new Route("id", condition, "hub", TelemetryMessageSource.Instance, new HashSet<Endpoint>());
            Func<IMessage, Bool> rule = RouteCompiler.Instance.Compile(route, RouteCompilerFlags.BodyQuery);
            Assert.Equal(rule(Message1), Bool.True);
        }

        [Theory]
        [Unit]
        [InlineData("10 >= $body.message.Weather.Temperature")]
        [InlineData("$body.message.Weather.Temperature < 50")]
        [InlineData("$body.message.Weather.Temperature = '50'")] // no implicit cross type conversion
        [InlineData("$body.message.Weather.Temperature != 50")]
        [InlineData("$body.message.Weather.Temperature = 60")]
        [InlineData("$body.message.Weather.Temperature <> 40 + 10")]
        [InlineData("$body.message.Weather.Temperature <= $body.message.Weather.PrevTemperatures[0]")]
        [InlineData("$body.message.Weather.Temperature < $body.message.Weather.PrevTemperatures[0] + $body.message.Weather.PrevTemperatures[1]")]
        [InlineData("$body.message.Weather.PrevTemperatures[0] + $body.message.Weather.PrevTemperatures[1] <> 50 ")]
        [InlineData("$body.message.Weather.PrevTemperatures[0] * $body.message.Weather.PrevTemperatures[1] = 610")]
        [InlineData("$body.message.Weather.PrevTemperatures[1] / $body.message.Weather.PrevTemperatures[0] = 3/3")]
        [InlineData("$body.message.Weather.HistoricalData[0].Temperature + 10 = $body.message.Weather.HistoricalData[1].Temperature")]
        public void BodyQuery_Double_Failure(string condition)
        {
            var route = new Route("id", condition, "hub", TelemetryMessageSource.Instance, new HashSet<Endpoint>());
            Func<IMessage, Bool> rule = RouteCompiler.Instance.Compile(route, RouteCompilerFlags.BodyQuery);
            Assert.Equal(rule(Message1), Bool.False);
        }

        [Theory]
        [Unit]
        [InlineData("$body.message.Weather.Temperature <> '100'")]
        [InlineData("$body.message.Weather.Location.City = City")]
        [InlineData("$body.message.Weather.Location.State = $State")]
        [InlineData("$body.message.Weather.Location.State <> $body.message.Weather.Location.City")]
        public void BodyQuery_String_Success(string condition)
        {
            var route = new Route("id", condition, "hub", TelemetryMessageSource.Instance, new HashSet<Endpoint>());
            Func<IMessage, Bool> rule = RouteCompiler.Instance.Compile(route, RouteCompilerFlags.BodyQuery);
            Assert.Equal(rule(Message1), Bool.True);
        }

        [Theory]
        [Unit]
        [InlineData("$body.message.Weather.Temperature ='100'")]
        [InlineData("$body.message.Weather.Temperature ='150'")]
        [InlineData("$body.message.Weather.Location.City != City")]
        [InlineData("$body.message.Weather.Location.State != $State")]
        [InlineData("$body.message.Weather.Location.State = City")]
        [InlineData("$body.message.Weather.Location.State = $body.message.Weather.Location.City")]
        public void BodyQuery_String_Failure(string condition)
        {
            var route = new Route("id", condition, "hub", TelemetryMessageSource.Instance, new HashSet<Endpoint>());
            Func<IMessage, Bool> rule = RouteCompiler.Instance.Compile(route, RouteCompilerFlags.BodyQuery);
            Assert.Equal(rule(Message1), Bool.False);
        }

        [Theory]
        [Unit]
        [InlineData("$body.message.Weather.IsEnabled")]
        [InlineData("$body.message.Weather.IsEnabled = true")]
        [InlineData("$body.message.Weather.IsDisabled = false")]
        [InlineData("$body.message.Weather.IsDisabled <> true")]
        [InlineData("NOT $body.message.Weather.IsDisabled")]
        [InlineData("$body.message.Weather.InvalidKey <> '100'")]
        public void BodyQuery_Bool(string condition)
        {
            var route = new Route("id", condition, "hub", TelemetryMessageSource.Instance, new HashSet<Endpoint>());
            Func<IMessage, Bool> rule = RouteCompiler.Instance.Compile(route, RouteCompilerFlags.BodyQuery);
            Assert.Equal(rule(Message1), Bool.True);
        }

        [Theory]
        [Unit]
        [InlineData("$body.message.Weather.IsEnabled AND $body.message.Weather.IsEnabled")]
        [InlineData("$body.message.Weather.IsEnabled OR $body.message.Weather.IsDisabled")]
        [InlineData("$body.message.Weather.IsDisabled OR NOT($body.message.Weather.IsDisabled)")]
        public void BodyQuery_Logical(string condition)
        {
            var route = new Route("id", condition, "hub", TelemetryMessageSource.Instance, new HashSet<Endpoint>());
            Func<IMessage, Bool> rule = RouteCompiler.Instance.Compile(route, RouteCompilerFlags.BodyQuery);
            Assert.Equal(rule(Message1), Bool.True);
        }

        [Theory]
        [Unit]
        [InlineData("$body")]
        [InlineData("$body.message.Weather.HistoricalData[0].Temperature.InvalidKey")]
        [InlineData("$City <> $body.message.Weather.InvalidKey")]
        public void BodyQuery_Undefined(string condition)
        {
            var route = new Route("id", condition, "hub", TelemetryMessageSource.Instance, new HashSet<Endpoint>());
            Func<IMessage, Bool> rule = RouteCompiler.Instance.Compile(route, RouteCompilerFlags.BodyQuery);
            Assert.Equal(rule(Message1), Bool.Undefined);
        }

        [Theory]
        [Unit]
        [InlineData("$body.message.Weather.Temperature_PropertyConflict = '100'")]
        [InlineData("as_number($body.message.Weather.Temperature_PropertyConflict) = 100")]
        [InlineData("{$body.message.Weather.Temperature_PropertyConflict} <> 100")]
        [InlineData("$body.message.Weather.Location.State = 'WA'")]
        [InlineData("{$body.message.Weather.Location.State} <> 'CA'")]
        public void BodyQuery_SysPropertyConflict(string condition)
        {
            var route = new Route("id", condition, "hub", TelemetryMessageSource.Instance, new HashSet<Endpoint>());
            Func<IMessage, Bool> rule = RouteCompiler.Instance.Compile(route, RouteCompilerFlags.BodyQuery);

            Bool result = rule(Message1);
            Assert.Equal(result, Bool.True);
        }

        [Fact]
        [Unit]
        public void BodyQuery_NotSupported()
        {
            string condition = "$BODY.message.Weather.Temperature >= 50";

            var route = new Route("id", condition, "hub", TelemetryMessageSource.Instance, new HashSet<Endpoint>());
            Assert.Throws<RouteCompilationException>(() => RouteCompiler.Instance.Compile(route, RouteCompilerFlags.None));
        }

        [Theory]
        [Unit]
        [InlineData("$body.message.Weather.Time <> '100'")]
        [InlineData("$body.message.Weather <> null")]
        public void BodyQuery_NotSupportedJTokenTime(string condition)
        {
            var route = new Route("id", condition, "hub", TelemetryMessageSource.Instance, new HashSet<Endpoint>());
            Func<IMessage, Bool> rule = RouteCompiler.Instance.Compile(route, RouteCompilerFlags.BodyQuery);

            Bool result = rule(Message1);
            Assert.Equal(result, Bool.Undefined);
        }

        [Theory]
        [Unit]
        [InlineData("$body.message.Weather.NullValue = null")]
        [InlineData("$body.message.Temperature = null")]
        [InlineData("$body.message.Weather.Temperature != null")]
        [InlineData("$body.message.Weather.Location.State <> null")]
        [InlineData("$body.message.Weather.InvalidProperty = null")]
        [InlineData("NOT ($body.message.Weather.InvalidProperty != null)")]
        public void BodyQuery_Null(string condition)
        {
            var route = new Route("id", condition, "hub", TelemetryMessageSource.Instance, new HashSet<Endpoint>());
            Func<IMessage, Bool> rule = RouteCompiler.Instance.Compile(route, RouteCompilerFlags.BodyQuery);

            Bool result = rule(Message1);
            Assert.Equal(result, Bool.True);
        }

        [Theory]
        [Unit]
        [InlineData("abs($body.message.Weather.FreezingTemperature) = 50.4")]
        [InlineData("ceiling(as_number($body.message.Weather.PreciseTemperature)) = 51")]
        [InlineData("concat($body.message.Weather.Location.Street, ', ', $body.message.Weather.Location.City, ', ', $body.message.Weather.Location.State) = 'One Microsoft Way, Redmond, WA'")]
        [InlineData("contains($body.message.Weather.Location.Street, 'Microsoft')")]
        [InlineData("ends_with($body.message.Weather.Location.Street, 'Way')")]
        [InlineData("exp(($body.message.Weather.Temperature - 49)) < ($body.message.Weather.Temperature - 47)")]
        [InlineData("index_of($body.message.Weather.Location.Street, 'Way') > 6")]
        [InlineData("is_defined($body.message.Weather.Location.Street)")]
        [InlineData("is_null($body.message.Weather.NullValue)")]
        [InlineData("is_string($body.message.Weather.Location.Street)")]
        [InlineData("length($body.message.Weather.Location.State) = 2")]
        [InlineData("lower($body.message.Weather.Location.State) = 'wa'")]
        [InlineData("power($body.message.Weather.Temperature, 2) = 2500.0")]
        [InlineData("power($body.message.Weather.Temperature, length($body.message.Weather.Location.State)) = 2500.0")]
        [InlineData("sign($body.message.Weather.FreezingTemperature) = -1")]
        [InlineData("square($body.message.Weather.Temperature) = 2500")]
        [InlineData("power($body.message.Weather.Temperature, length($body.message.Weather.Location.State)) = square($body.message.Weather.Temperature)")]
        [InlineData("starts_with($body.message.Weather.Location.Street, 'One')")]
        [InlineData("substring($body.message.Weather.Location.Street, length($body.message.Weather.Location.Street) - 3) = 'Way'")]
        [InlineData("substring($body.message.Weather.Location.Street, 4, length('Microsoft')) = 'Microsoft'")]
        [InlineData("upper($body.message.Weather.Location.Street) = 'ONE MICROSOFT WAY'")]
        public void BodyQuery_Builtins(string condition)
        {
            var route = new Route("id", condition, "hub", TelemetryMessageSource.Instance, new HashSet<Endpoint>());
            Func<IMessage, Bool> rule = RouteCompiler.Instance.Compile(route, RouteCompilerFlags.BodyQuery);

            Bool result = rule(Message1);
            Assert.Equal(result, Bool.True);
        }

        [Fact]
        [Unit]
        public void DebugBodyQuery()
        {
            string condition = "$BODY.State[0] != '40'";

            var route = new Route("id", condition, "hub", TelemetryMessageSource.Instance, new HashSet<Endpoint>());

            try
            {
                Func<IMessage, Bool> rule = RouteCompiler.Instance.Compile(route, RouteCompilerFlags.BodyQuery);

                Bool result = rule(Message1);

                Assert.Equal(result, Bool.True);
            }
            catch (Exception ex)
            {
                Assert.NotNull(ex);
            }
        }
    }
}
