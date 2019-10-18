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
        // using ThreadStatic to allow parallel tests. Note, that this has the pitfall when using
        // async providers which may cause a builder to access these properties from a different
        // thread.
        [ThreadStatic]
        private static int deviceCounter;

        [ThreadStatic]
        private static int moduleCounter;

        [ThreadStatic]
        private static string iotHubName;

        public static void StartNewContext()
        {
            TestContext.deviceCounter = 0;
            TestContext.moduleCounter = 0;
            TestContext.iotHubName = null;
        }

        public static string DeviceId => $"device-{TestContext.deviceCounter + 1}";
        public static string ModuleId => $"device-{TestContext.deviceCounter + 1}/module-{TestContext.moduleCounter + 1}";
        public static string IotHubName => TestContext.iotHubName ?? "test-hub";

        public static TextContextBuilder WithNextDevice()
        {
            Interlocked.Increment(ref TestContext.deviceCounter);
            return new TextContextBuilder();
        }

        public static TextContextBuilder WithNextModule()
        {
            Interlocked.Increment(ref TestContext.moduleCounter);
            return new TextContextBuilder();
        }

        public static TextContextBuilder WithIotHubName(string iotHubName)
        {
            Volatile.Write(ref TestContext.iotHubName, iotHubName);
            return new TextContextBuilder();
        }

        internal class TextContextBuilder
        {
            public TextContextBuilder WithNextDevice()
            {
                TestContext.WithNextDevice();
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
