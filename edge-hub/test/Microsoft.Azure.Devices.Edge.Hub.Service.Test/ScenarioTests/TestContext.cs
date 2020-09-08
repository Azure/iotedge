// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Service.Test.ScenarioTests
{
    using System;
    using System.Threading;

    /// <summary>
    /// The purpose of this class is to share variables among object builders. Some variables
    /// must be synchronized, e.g. device id/module id, hub name and it would be too painful
    /// to pass them down the builder chain.
    /// </summary>
    internal class TestContext
    {
        private static AsyncLocal<int> deviceCounter = new AsyncLocal<int>();
        private static AsyncLocal<int> moduleCounter = new AsyncLocal<int>();
        private static AsyncLocal<string> iotHubName = new AsyncLocal<string>();

        public static void StartNewContext()
        {
            TestContext.deviceCounter.Value = 0;
            TestContext.moduleCounter.Value = 0;
            TestContext.iotHubName.Value = null;
        }

        public static string DeviceId => $"device-{TestContext.deviceCounter.Value + 1}";
        public static string ModuleId => $"device-{TestContext.deviceCounter.Value + 1}/module-{TestContext.moduleCounter.Value + 1}";
        public static string IotHubName => TestContext.iotHubName.Value ?? "test-hub";

        public static TextContextBuilder WithNextDevice()
        {
            TestContext.deviceCounter.Value = TestContext.deviceCounter.Value + 1;
            return new TextContextBuilder();
        }

        public static TextContextBuilder WithNextModule()
        {
            TestContext.moduleCounter.Value = TestContext.moduleCounter.Value + 1;
            return new TextContextBuilder();
        }

        public static TextContextBuilder WithIotHubName(string iotHubName)
        {
            TestContext.iotHubName.Value = iotHubName;
            return new TextContextBuilder();
        }

        internal class TextContextBuilder
        {
            public TextContextBuilder WithNextDevice()
            {
                TestContext.WithNextDevice();
                TestContext.moduleCounter.Value = 0;
                return this;
            }

            public TextContextBuilder WithNextModule()
            {
                TestContext.WithNextModule();
                return this;
            }

            public TextContextBuilder WithIotHubName(string iotHubName)
            {
                TestContext.WithIotHubName(iotHubName);
                return this;
            }
        }
    }
}
