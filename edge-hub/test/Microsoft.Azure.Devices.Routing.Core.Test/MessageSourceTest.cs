// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core.Test
{
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Microsoft.Azure.Devices.Routing.Core.MessageSources;
    using Xunit;

    [Unit]
    public class MessageSourceTest
    {
        static readonly ModuleMessageSource ModuleMessageSource = ModuleMessageSource.Create("ModId1", "Op1");

        [Fact]
        public void TestTelemetryMessageSource()
        {
            Assert.True(TelemetryMessageSource.Instance.IsTelemetry());

            TelemetryMessageSource telemetryMessageSource = TelemetryMessageSource.Instance;
            Assert.True(TelemetryMessageSource.Instance.Match(telemetryMessageSource));
            
            Assert.False(TelemetryMessageSource.Instance.Match(TwinChangeEventMessageSource.Instance));
            
            Assert.False(TelemetryMessageSource.Instance.Match(ModuleMessageSource));          
        }        

        [Fact]
        public void TestModuleMessageSource()
        {
            Assert.True(ModuleMessageSource.IsTelemetry());

            Assert.False(ModuleMessageSource.Match(TelemetryMessageSource.Instance));
            Assert.False(ModuleMessageSource.Match(TwinChangeEventMessageSource.Instance));

            ModuleMessageSource matchingModuleMessageSource = ModuleMessageSource.Create("ModId1", "Op1");
            Assert.True(ModuleMessageSource.Match(matchingModuleMessageSource));
            Assert.True(matchingModuleMessageSource.Match(ModuleMessageSource));

            ModuleMessageSource unmatchedModuleMessageSource = ModuleMessageSource.Create("ModId2", "Op2");
            Assert.False(ModuleMessageSource.Match(unmatchedModuleMessageSource));
            Assert.False(unmatchedModuleMessageSource.Match(ModuleMessageSource));
        }

        [Fact]
        public void TestTwinChangeEventMessageSource()
        {
            Assert.False(TwinChangeEventMessageSource.Instance.IsTelemetry());

            TwinChangeEventMessageSource twinChangeEventMessageSource = TwinChangeEventMessageSource.Instance;
            Assert.True(TwinChangeEventMessageSource.Instance.Match(twinChangeEventMessageSource));

            Assert.False(TwinChangeEventMessageSource.Instance.Match(TelemetryMessageSource.Instance));

            Assert.False(TwinChangeEventMessageSource.Instance.Match(ModuleMessageSource));

            CustomMessageSource customMessageSource1 = CustomMessageSource.Create("/twinChangeNotifications");
            Assert.True(TwinChangeEventMessageSource.Instance.Match(customMessageSource1));
        }

        [Theory]
        [InlineData("/")]
        [InlineData("/*")]
        [InlineData("/messages")]
        [InlineData("/messages/*")]
        [InlineData("/messages/events")]
        [InlineData("/messages/events/")]
        public void TestTelemetryMessageSourcePatternMatch(string source)
        {
            CustomMessageSource customMessageSource = CustomMessageSource.Create(source);
            Assert.True(customMessageSource.Match(TelemetryMessageSource.Instance));
        }

        [Theory]
        [InlineData("/twinChangeNotifications")]
        [InlineData("/foo/*")]
        [InlineData("/message")]
        [InlineData("/*/messages/*")]
        [InlineData("/messages/event")]
        [InlineData("/messages/modules")]
        [InlineData("/messages/modules/*")]
        [InlineData("/messages/modules/ModId1")]
        [InlineData("/messages/modules/ModId1/*")]
        public void TestTelemetryMessageSourcePatternNoMatch(string source)
        {
            CustomMessageSource customMessageSource = CustomMessageSource.Create(source);
            Assert.False(customMessageSource.Match(TelemetryMessageSource.Instance));
        }

        [Theory]
        [InlineData("/")]
        [InlineData("/*")]
        [InlineData("/twinChangeNotifications")]
        [InlineData("/twinChangeNotifications/*")]
        public void TestTwinChangeEventMessageSourcePatternMatch(string source)
        {
            CustomMessageSource customMessageSource = CustomMessageSource.Create(source);
            Assert.True(customMessageSource.Match(TwinChangeEventMessageSource.Instance));
        }

        [Theory]
        [InlineData("/messages")]
        [InlineData("/foo/*")]
        [InlineData("/message")]
        [InlineData("/*/messages/*")]
        [InlineData("/messages/modules")]
        [InlineData("/messages/modules/*")]
        [InlineData("/messages/modules/ModId1")]
        [InlineData("/messages/modules/ModId1/*")]
        public void TestTwinChangeEventMessageSourcePatternNoMatch(string source)
        {
            CustomMessageSource customMessageSource = CustomMessageSource.Create(source);
            Assert.False(customMessageSource.Match(TwinChangeEventMessageSource.Instance));
        }

        [Theory]
        [InlineData("/")]
        [InlineData("/*")]
        [InlineData("/messages")]
        [InlineData("/messages/*")]
        [InlineData("/messages/modules")]
        [InlineData("/messages/modules/*")]
        [InlineData("/messages/modules/ModId1")]
        [InlineData("/messages/modules/ModId1/*")]
        [InlineData("/messages/modules/ModId1/outputs")]
        [InlineData("/messages/modules/ModId1/outputs/*")]
        [InlineData("/messages/modules/ModId1/outputs/Op1")]        
        public void TestModuleMessageSourcePatternMatch(string source)
        {
            CustomMessageSource customMessageSource = CustomMessageSource.Create(source);
            Assert.True(customMessageSource.Match(ModuleMessageSource));
        }

        [Theory]
        [InlineData("/foo")]
        [InlineData("/*/bar/*")]
        [InlineData("/modulemessages")]
        [InlineData("/messages/events")]
        [InlineData("/messages/modules/events")]
        [InlineData("/messages/modules/ModId2")]
        [InlineData("/messages/modules/ModId2/*")]
        [InlineData("/messages/modules/ModId1/outputs/Op2")]
        public void TestModuleMessageSourcePatternNoMatch(string source)
        {
            CustomMessageSource customMessageSource = CustomMessageSource.Create(source);
            Assert.False(customMessageSource.Match(ModuleMessageSource));
        }
    }
}