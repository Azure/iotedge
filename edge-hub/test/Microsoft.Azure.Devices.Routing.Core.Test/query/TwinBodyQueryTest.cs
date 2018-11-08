// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
/*
//// ---------------------------------------------------------------
//// Copyright (c) Microsoft Corporation. All rights reserved.
//// ---------------------------------------------------------------

//namespace Microsoft.Azure.Devices.Routing.Core.Test.Query
//{
//    using System;
//    using System.CodeDom.Compiler;
//    using System.Collections.Generic;
//    using System.Text;
//    using Microsoft.Azure.Devices.Common.Api;
//    using Microsoft.Azure.Devices.DeviceManagement.Model;
//    using Microsoft.Azure.Devices.Routing.Core.Query;
//    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
//    using Xunit;

//    public class TwinBodyQueryTest: RoutingUnitTestBase
//    {
//        public TwinBodyQueryTest()
//        {
//            Message1 = GenerateDefaultTwinMessage();
//        }

//        IMessage Message1;
//        IMessage GenerateDefaultTwinMessage()
//        {
//            var twinInfo = new DeviceTwinInfo();
//            twinInfo.Tags = new DataObject();
//            twinInfo.DesiredProperties = new DeviceTwinProperties();
//            twinInfo.ReportedProperties = new DeviceTwinProperties();
//            var weather = new DataObject();
//            weather["Temperature"] = 50;
//            weather["FreezingTemperature"] = -50.4;
//            weather["PreciseTemperature"] = 50.4;
//            weather["Temperature_PropertyConflict"] = 50;
//            weather["NullValue"] = null;
//            weather["Time"] = DateTime.Parse("2017-03-09T00:00:00.000Z");
//            weather["IsEnabled"] = true;
//            weather["IsDisabled"] = false;
//            weather["PrevTemperatures_0"] = 20;
//            weather["PrevTemperatures_1"] = 30;
//            weather["PrevTemperatures_2"] = 40;
//            var location = new DataObject();
//            location["Street"] = "One Microsoft Way";
//            location["City"] = "Redmond";
//            location["State"] = "WA";

//            var histori0 = new DataObject();
//            histori0["Month"] = "Feb";
//            histori0["Temperature"] = 40;

//            var histori1 = new DataObject();
//            histori1["Month"] = "Jan";
//            histori1["Temperature"] = 30;

//            weather["HistoricalData_0"] = histori0;
//            weather["HistoricalData_1"] = histori1;
//            weather["Location"] = location;
//            twinInfo.Tags["Weather"] = weather;

//            IMessage twinMessage = new TwinMessage(TwinChangeEventMessageSource.Instance, twinInfo,
//                new Dictionary<string, string>
//            {
//                { "City", "Redmond" },
//                { "$State", "WA" },
//                { "$body.tags.Weather.Temperature_PropertyConflict", "100" },
//                { "IntegerProperty", "110" },
//            },
//            new Dictionary<string, string>
//            {
//                { "State", "CA" },
//                { "$body.tags.Weather.Location.State", "CA" },
//                {  SystemProperties.ContentEncoding, "UTF-8" },
//                {  SystemProperties.ContentType, Constants.SystemPropertyValues.ContentType.Json },
//            });

//            return twinMessage;
//        }

//        [Theory, Unit]
//        [InlineData("50 >= $body.tags.Weather.Temperature")]
//        [InlineData("$body.tags.Weather.Temperature >= 50")]
//        [InlineData("$body.tags.Weather.Temperature <= 50")]
//        [InlineData("$body.tags.Weather.Temperature = 50")]
//        [InlineData("$body.tags.Weather.Temperature != 60")]
//        [InlineData("$body.tags.Weather.Temperature <> 500")]
//        [InlineData("$body.tags.Weather.Temperature = 40 + 10")]
//        [InlineData("$body.tags.Weather.Temperature >= $body.tags.Weather.PrevTemperatures_0")]
//        [InlineData("$body.tags.Weather.Temperature >= $body.tags.Weather.PrevTemperatures_0 + $body.tags.Weather.PrevTemperatures_1 ")]
//        [InlineData("$body.tags.Weather.PrevTemperatures_0 + $body.tags.Weather.PrevTemperatures_1= 50 ")]
//        [InlineData("$body.tags.Weather.PrevTemperatures_1 * $body.tags.Weather.PrevTemperatures_2 = 1200")]
//        [InlineData("$body.tags.Weather.PrevTemperatures_1 / $body.tags.Weather.PrevTemperatures_0 = 1.5")]
//        [InlineData("$body.tags.Weather.HistoricalData_0.Temperature - 10 = $body.tags.Weather.HistoricalData_1.Temperature")]
//        [InlineData("$body.tags.Weather.HistoricalData_0.Month = 'Feb'")]
//        public void BodyQuery_Double_Success(string condition)
//        {
//            var route = new Route("id", condition, "hub", TwinChangeEventMessageSource.Instance, new HashSet<Endpoint>());
//            Func<IMessage, Bool> rule = RouteCompiler.Instance.Compile(route, RouteCompilerFlags.BodyQuery);
//            Assert.Equal(rule(Message1), Bool.True);
//        }

//        [Theory, Unit]
//        [InlineData("10 >= $body.tags.Weather.Temperature")]
//        [InlineData("$body.tags.Weather.Temperature < 50")]
//        [InlineData("$body.tags.Weather.Temperature = '50'")] // no implicit cross type conversion
//        [InlineData("$body.tags.Weather.Temperature != 50")]
//        [InlineData("$body.tags.Weather.Temperature = 60")]
//        [InlineData("$body.tags.Weather.Temperature <> 40 + 10")]
//        [InlineData("$body.tags.Weather.Temperature <= $body.tags.Weather.PrevTemperatures_0")]
//        [InlineData("$body.tags.Weather.Temperature < $body.tags.Weather.PrevTemperatures_0 + $body.tags.Weather.PrevTemperatures_1")]
//        [InlineData("$body.tags.Weather.PrevTemperatures_0 + $body.tags.Weather.PrevTemperatures_1 <> 50 ")]
//        [InlineData("$body.tags.Weather.PrevTemperatures_0 * $body.tags.Weather.PrevTemperatures_1 = 610")]
//        [InlineData("$body.tags.Weather.PrevTemperatures_1 / $body.tags.Weather.PrevTemperatures_0 = 3/3")]
//        [InlineData("$body.tags.Weather.HistoricalData_0.Temperature + 10 = $body.tags.Weather.HistoricalData_1.Temperature")]
//        public void BodyQuery_Double_Failure(string condition)
//        {
//            var route = new Route("id", condition, "hub", TwinChangeEventMessageSource.Instance, new HashSet<Endpoint>());
//            Func<IMessage, Bool> rule = RouteCompiler.Instance.Compile(route, RouteCompilerFlags.BodyQuery);
//            Assert.Equal(rule(Message1), Bool.False);
//        }

//        [Theory, Unit]
//        [InlineData("$body.tags.Weather.Temperature <> '100'")]
//        [InlineData("$body.tags.Weather.Location.City = City")]
//        [InlineData("$body.tags.Weather.Location.State = $State")]
//        [InlineData("$body.tags.Weather.Location.State <> $body.tags.Weather.Location.City")]
//        public void BodyQuery_String_Success(string condition)
//        {
//            var route = new Route("id", condition, "hub", TwinChangeEventMessageSource.Instance, new HashSet<Endpoint>());
//            Func<IMessage, Bool> rule = RouteCompiler.Instance.Compile(route, RouteCompilerFlags.BodyQuery);
//            Assert.Equal(rule(Message1), Bool.True);
//        }

//        [Theory, Unit]
//        [InlineData("$body.tags.Weather.Temperature ='100'")]
//        [InlineData("$body.tags.Weather.Temperature ='150'")]
//        [InlineData("$body.tags.Weather.Location.City != City")]
//        [InlineData("$body.tags.Weather.Location.State != $State")]
//        [InlineData("$body.tags.Weather.Location.State = City")]
//        [InlineData("$body.tags.Weather.Location.State = $body.tags.Weather.Location.City")]
//        public void BodyQuery_String_Failure(string condition)
//        {
//            var route = new Route("id", condition, "hub", MessageSource.TwinChangeEvents, new HashSet<Endpoint>());
//            Func<IMessage, Bool> rule = RouteCompiler.Instance.Compile(route, RouteCompilerFlags.BodyQuery);
//            Assert.Equal(rule(Message1), Bool.False);
//        }

//        [Theory, Unit]
//        [InlineData("$body.tags.Weather.IsEnabled")]
//        [InlineData("$body.tags.Weather.IsEnabled = true")]
//        [InlineData("$body.tags.Weather.IsDisabled = false")]
//        [InlineData("$body.tags.Weather.IsDisabled <> true")]
//        [InlineData("NOT $body.tags.Weather.IsDisabled")]
//        [InlineData("$body.tags.Weather.InvalidKey <> '100'")]
//        public void BodyQuery_Bool(string condition)
//        {
//            var route = new Route("id", condition, "hub", MessageSource.TwinChangeEvents, new HashSet<Endpoint>());
//            Func<IMessage, Bool> rule = RouteCompiler.Instance.Compile(route, RouteCompilerFlags.BodyQuery);
//            Assert.Equal(rule(Message1), Bool.True);
//        }

//        [Theory, Unit]
//        [InlineData("$body.tags.Weather.IsEnabled AND $body.tags.Weather.IsEnabled")]
//        [InlineData("$body.tags.Weather.IsEnabled OR $body.tags.Weather.IsDisabled")]
//        [InlineData("$body.tags.Weather.IsDisabled OR NOT($body.tags.Weather.IsDisabled)")]
//        public void BodyQuery_Logical(string condition)
//        {
//            var route = new Route("id", condition, "hub", MessageSource.TwinChangeEvents, new HashSet<Endpoint>());
//            Func<IMessage, Bool> rule = RouteCompiler.Instance.Compile(route, RouteCompilerFlags.BodyQuery);
//            Assert.Equal(rule(Message1), Bool.True);
//        }

//        [Theory, Unit]
//        [InlineData("$body")]
//        [InlineData("$body.tags.Weather.HistoricalData_0.Temperature.InvalidKey")]
//        [InlineData("$City <> $body.tags.Weather.InvalidKey")]
//        public void BodyQuery_Undefined(string condition)
//        {
//            var route = new Route("id", condition, "hub", MessageSource.TwinChangeEvents, new HashSet<Endpoint>());
//            Func<IMessage, Bool> rule = RouteCompiler.Instance.Compile(route, RouteCompilerFlags.BodyQuery);
//            Assert.Equal(rule(Message1), Bool.Undefined);
//        }

//        [Theory, Unit]
//        [InlineData("$body.tags.Weather.Temperature_PropertyConflict = '100'")]
//        [InlineData("as_number($body.tags.Weather.Temperature_PropertyConflict) = 100")]
//        [InlineData("{$body.tags.Weather.Temperature_PropertyConflict} <> 100")]
//        [InlineData("$body.tags.Weather.Location.State = 'WA'")]
//        [InlineData("{$body.tags.Weather.Location.State} <> 'CA'")]
//        public void BodyQuery_SysPropertyConflict(string condition)
//        {
//            var route = new Route("id", condition, "hub", MessageSource.TwinChangeEvents, new HashSet<Endpoint>());
//            Func<IMessage, Bool> rule = RouteCompiler.Instance.Compile(route, RouteCompilerFlags.BodyQuery);

//            Bool result = rule(Message1);
//            Assert.Equal(result, Bool.True);
//        }

//        [Fact, Unit]
//        public void BodyQuery_NotSupported()
//        {
//            string condition = "$body.tags.Weather.Temperature >= 50";

//            var route = new Route("id", condition, "hub", MessageSource.TwinChangeEvents, new HashSet<Endpoint>());
//            Assert.Throws<RouteCompilationException>(() => RouteCompiler.Instance.Compile(route, RouteCompilerFlags.None));
//        }

//        [Theory, Unit]
//        [InlineData("$body.tags.Weather.Time <> '100'")]
//        [InlineData("$body.tags.Weather <> null")]
//        public void BodyQuery_NotSupportedJTokenTime(string condition)
//        {
//            var route = new Route("id", condition, "hub", MessageSource.TwinChangeEvents, new HashSet<Endpoint>());
//            Func<IMessage, Bool> rule = RouteCompiler.Instance.Compile(route, RouteCompilerFlags.BodyQuery);

//            Bool result = rule(Message1);
//            Assert.Equal(result, Bool.Undefined);
//        }

//        [Theory, Unit]
//        [InlineData("NOT(is_defined($body.tags.Weather.NullValue))")]
//        [InlineData("$body.tags.Temperature = null")]
//        [InlineData("$body.tags.Weather.Temperature != null")]
//        [InlineData("$body.tags.Weather.Location.State <> null")]
//        [InlineData("$body.tags.Weather.InvalidProperty = null")]
//        [InlineData("NOT ($body.tags.Weather.InvalidProperty != null)")]
//        public void BodyQuery_Null(string condition)
//        {
//            var route = new Route("id", condition, "hub", MessageSource.TwinChangeEvents, new HashSet<Endpoint>());
//            Func<IMessage, Bool> rule = RouteCompiler.Instance.Compile(route, RouteCompilerFlags.BodyQuery);

//            Bool result = rule(Message1);
//            Assert.Equal(result, Bool.True);
//        }

//        [Theory, Unit]
//        [InlineData("abs($body.tags.Weather.FreezingTemperature) = 50.4")]
//        [InlineData("ceiling(as_number($body.tags.Weather.PreciseTemperature)) = 51")]
//        [InlineData("concat($body.tags.Weather.Location.Street, ', ', $body.tags.Weather.Location.City, ', ', $body.tags.Weather.Location.State) = 'One Microsoft Way, Redmond, WA'")]
//        [InlineData("contains($body.tags.Weather.Location.Street, 'Microsoft')")]
//        [InlineData("ends_with($body.tags.Weather.Location.Street, 'Way')")]
//        [InlineData("exp(($body.tags.Weather.Temperature - 49)) < ($body.tags.Weather.Temperature - 47)")]
//        [InlineData("index_of($body.tags.Weather.Location.Street, 'Way') > 6")]
//        [InlineData("is_defined($body.tags.Weather.Location.Street)")]
//        [InlineData("NOT(is_defined($body.tags.Weather.NullValue))")]
//        [InlineData("is_string($body.tags.Weather.Location.Street)")]
//        [InlineData("length($body.tags.Weather.Location.State) = 2")]
//        [InlineData("lower($body.tags.Weather.Location.State) = 'wa'")]
//        [InlineData("power($body.tags.Weather.Temperature, 2) = 2500.0")]
//        [InlineData("power($body.tags.Weather.Temperature, length($body.tags.Weather.Location.State)) = 2500.0")]
//        [InlineData("sign($body.tags.Weather.FreezingTemperature) = -1")]
//        [InlineData("square($body.tags.Weather.Temperature) = 2500")]
//        [InlineData("power($body.tags.Weather.Temperature, length($body.tags.Weather.Location.State)) = square($body.tags.Weather.Temperature)")]
//        [InlineData("starts_with($body.tags.Weather.Location.Street, 'One')")]
//        [InlineData("substring($body.tags.Weather.Location.Street, length($body.tags.Weather.Location.Street) - 3) = 'Way'")]
//        [InlineData("substring($body.tags.Weather.Location.Street, 4, length('Microsoft')) = 'Microsoft'")]
//        [InlineData("upper($body.tags.Weather.Location.Street) = 'ONE MICROSOFT WAY'")]
//        public void BodyQuery_Builtins(string condition)
//        {
//            var route = new Route("id", condition, "hub", MessageSource.TwinChangeEvents, new HashSet<Endpoint>());
//            Func<IMessage, Bool> rule = RouteCompiler.Instance.Compile(route, RouteCompilerFlags.BodyQuery);

//            Bool result = rule(Message1);
//            Assert.Equal(result, Bool.True);
//        }

//        [Fact, Unit]
//        public void DebugBodyQuery()
//        {
//            string condition = "$BODY.State_0 != '40'";

//            var route = new Route("id", condition, "hub", MessageSource.TwinChangeEvents, new HashSet<Endpoint>());

//            try
//            {
//                Func<IMessage, Bool> rule = RouteCompiler.Instance.Compile(route, RouteCompilerFlags.BodyQuery);

//                Bool result = rule(Message1);

//                Assert.NotNull(result);
//                Assert.Equal(result, Bool.True);
//            }
//            catch (Exception ex)
//            {
//                Assert.NotNull(ex);
//            }
//        }
//    }
//}
*/
