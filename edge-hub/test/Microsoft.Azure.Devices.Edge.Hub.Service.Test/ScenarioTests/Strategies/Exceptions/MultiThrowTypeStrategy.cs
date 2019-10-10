namespace Microsoft.Azure.Devices.Edge.Hub.Service.Test.ScenarioTests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.Devices.Edge.Hub.Core;

    public class MultiThrowTypeStrategy : IMessageConverter<Exception>
    {
        private Random random = new Random(769834);

        private List<Type> exceptionSuite = new List<Type>();

        public MultiThrowTypeStrategy()
        {
        }

        public static MultiThrowTypeStrategy Create()
        {
            return new MultiThrowTypeStrategy();
        }

        // this can be called multiple times and merges
        public MultiThrowTypeStrategy WithExceptionSuite(IEnumerable<Type> exceptions)
        {
            if (exceptions.Any(e => !typeof(Exception).IsAssignableFrom(e)))
                throw new ArgumentException("Only types of Exception can be used in Exception Suite");

            var existingExceptions = new HashSet<Type>(this.exceptionSuite);

            foreach (var exception in exceptions)
                existingExceptions.Add(exception);

            this.exceptionSuite = existingExceptions.ToList();

            return this;
        }

        public Exception Convert(IMessage message)
        {
            var errorMessage = "test-error";

            if (message.SystemProperties.TryGetValue(SystemProperties.MessageId, out string id))
            {
                errorMessage += $" for msg: {id}";
            }

            var toThrow = this.CreateInstance(this.PickException(), errorMessage);
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

            var toThrow = this.CreateInstance(this.PickException(), errorMessage);
            toThrow.Data["Messages"] = messages;

            return toThrow;
        }

        private Type PickException()
        {
            lock (this.random)
            {
                var index = this.random.Next(exceptionSuite.Count);
                return this.exceptionSuite[index];
            }
        }

        private Exception CreateInstance(Type exceptionType, string message)
        {
            return Activator.CreateInstance(exceptionType, message) as Exception;
        }

    }
}
