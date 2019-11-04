// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Service.Test.ScenarioTests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;

    public class SimpleMessageGeneratingStrategyBase
    {
        private int messageCounter = 0;

        private int minBodySize = 20;
        private int maxBodySize = 200;

        private string[] properties = { "test_prop1", "test_prop2" };

        protected SimpleMessageGeneratingStrategyBase()
        {
        }

        protected void WithBodySize(int minBodySize, int maxBodySize)
        {
            this.minBodySize = minBodySize;
            this.maxBodySize = maxBodySize;
        }

        protected (byte[], Dictionary<string, string>, Dictionary<string, string>) GetComponents()
        {
            var msgCounter = Interlocked.Increment(ref this.messageCounter);

            var body = this.GetBody(msgCounter);
            var properties = this.GetProperties(msgCounter);
            var systemProperties = this.GetSystemProperties(msgCounter);

            return (body, properties, systemProperties);
        }

        private byte[] GetBody(int msgCounter)
        {
            var random = new Random(msgCounter * 23);

            var bodySize = default(int);
            var result = default(byte[]);

            bodySize = random.Next(this.minBodySize, this.maxBodySize);

            result = new byte[bodySize];
            random.NextBytes(result);

            return result;
        }

        private Dictionary<string, string> GetProperties(int msgCounter)
        {
            var random = new Random(msgCounter * 17);
            var result = new Dictionary<string, string>();

            Array.ForEach(this.properties, p => result.Add(p, Utils.RandomString(random, random.Next(5, 10))));

            result.Add("counter", msgCounter.ToString());
            result.Add("generated", DateTime.Now.ToString("MM/dd-HH:mm:ss.fffffff"));

            return result;
        }

        private Dictionary<string, string> GetSystemProperties(int msgCounter)
        {
            var result = new Dictionary<string, string>()
            {
                [Core.SystemProperties.EdgeMessageId] = Guid.NewGuid().ToString(),
                [Core.SystemProperties.MessageId] = "test-msg-" + msgCounter.ToString(),
                [Core.SystemProperties.ConnectionDeviceId] = TestContext.DeviceId
            };

            return result;
        }
    }
}
