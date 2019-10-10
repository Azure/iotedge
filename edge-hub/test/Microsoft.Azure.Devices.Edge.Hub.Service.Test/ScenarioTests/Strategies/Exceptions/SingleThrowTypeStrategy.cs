namespace Microsoft.Azure.Devices.Edge.Hub.Service.Test.ScenarioTests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.Devices.Client.Exceptions;
    using Microsoft.Azure.Devices.Edge.Hub.Core;

    public class SingleThrowTypeStrategy : IMessageConverter<Exception>
    {
        private Type exceptionType = typeof(IotHubException);

        public static SingleThrowTypeStrategy Create()
        {
            return new SingleThrowTypeStrategy();
        }

        public SingleThrowTypeStrategy WithType<T>()
            where T : Exception
        {
            this.exceptionType = typeof(T);
            return this;
        }

        public Exception Convert(IMessage message)
        {
            var errorMessage = "test-error";

            if (message.SystemProperties.TryGetValue(SystemProperties.MessageId, out string id))
            {
                errorMessage += $" for msg: {id}";
            }

            var toThrow = this.CreateInstance(errorMessage);
            toThrow.Data["Messages"] = new IMessage[] { message };

            return toThrow;
        }

        public Exception Convert(IEnumerable<IMessage> messages)
        {
            var errorMessage = "test-error";

            if (messages.Any() && messages.First().SystemProperties.TryGetValue(SystemProperties.MessageId, out string id))
            {
                errorMessage += $" for msg: {id}";
                if (messages.Count() > 1)
                {
                    errorMessage += $" and {messages.Count() - 1} more...";
                }
            }

            var toThrow = this.CreateInstance(errorMessage);
            toThrow.Data["Messages"] = messages;

            return toThrow;
        }

        private Exception CreateInstance(string message)
        {
            return Activator.CreateInstance(this.exceptionType, message) as Exception;
        }
    }
}
