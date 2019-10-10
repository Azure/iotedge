using Microsoft.Azure.Devices.Routing.Core.MessageSources;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Microsoft.Azure.Devices.Edge.Hub.Service.Test.ScenarioTests
{
    // this class has several properties hardcoded and could be made customizable (e.g message properties)
    // feel free to add code for your purposes
    public class SimpleMessageGeneratingStrategy : IMessageGeneratingStrategy
    {
        private int messageCounter = 0;

        private int minBodySize = 20;
        private int maxBodySize = 200;

        private string[] properties = { "test_prop1", "test_prop2" };

        public static SimpleMessageGeneratingStrategy Create()
        {
            return new SimpleMessageGeneratingStrategy();
        }

        public SimpleMessageGeneratingStrategy WithBodySize(int minBodySize, int maxBodySize)
        {
            this.minBodySize = minBodySize;
            this.maxBodySize = maxBodySize;

            return this;
        }

        public Routing.Core.Message Next()
        {
            var msgCounter = Interlocked.Increment(ref messageCounter);

            var body = this.getBody(msgCounter);
            var properties = this.getProperties(msgCounter);
            var systemProperties = this.getSystemProperties(msgCounter);

            var result = new Routing.Core.Message(TelemetryMessageSource.Instance, body, properties, systemProperties);

            return result;
        }

        private byte[] getBody(int msgCounter)
        {
            var random = new Random(msgCounter * 23);

            var bodySize = default(int);
            var result = default(byte[]);

            bodySize = random.Next(this.minBodySize, this.minBodySize);

            result = new byte[bodySize];
            random.NextBytes(result);

            return result;
        }

        private Dictionary<string, string> getProperties(int msgCounter)
        {
            var random = new Random(msgCounter * 17);
            var result = new Dictionary<string, string>();

            Array.ForEach(properties, p => result.Add(p, RandomString(random, random.Next(5, 10))));

            result.Add("counter", msgCounter.ToString());
            result.Add("generated", DateTime.Now.ToString("MM/dd-HH:mm:ss.fffffff"));

            return result;
        }

        private Dictionary<string, string> getSystemProperties(int msgCounter)
        {
            var result = new Dictionary<string, string>()
            {
                [Core.SystemProperties.EdgeMessageId] = Guid.NewGuid().ToString(),
                [Core.SystemProperties.MessageId] = "test-msg-" + (msgCounter).ToString(),
                [Core.SystemProperties.ConnectionDeviceId] = "test-device"
            };

            return result;
        }

        public static string RandomString(Random random, int length)
        {
            const string chars = "abcdefghijklmnopqrstuvwxyz0123456789";
            return new string(Enumerable.Repeat(chars, length)
              .Select(s => s[random.Next(s.Length)]).ToArray());
        }
    }
}
