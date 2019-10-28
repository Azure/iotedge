using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.Devices.Edge.Agent.Core.Metrics;
using Microsoft.Azure.Devices.Edge.Util.Metrics;
using Microsoft.Azure.Devices.Edge.Util.Metrics.NullMetrics;
using Microsoft.Azure.Devices.Edge.Util.Test.Common;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Newtonsoft.Json;
using Xunit;

namespace Microsoft.Azure.Devices.Edge.Agent.Core.Test.Metrics
{
    [Unit]
    public class ExceptionCounterTest
    {
        [Fact]
        public void CountsExceptions()
        {
            /* setup */
            Dictionary<string, long> result = MockCounter();
            Dictionary<string, long> expected = new Dictionary<string, long>();

            Dictionary<Type, string> recognizedExceptions = new Dictionary<Type, string>
            {
                { typeof(JsonSerializationException), "json_serialization" },
                { typeof(ArgumentException), "argument" },
            };

            HashSet<Type> ignoredExeptions = new HashSet<Type>
            {
                typeof(TaskCanceledException),
                typeof(OperationCanceledException),
            };
            new ExceptionCounter(recognizedExceptions, ignoredExeptions);

            /* test */
            Assert.Equal(expected, result);

            ThrowAndCatch(new ArgumentException());
            expected["argument"] = 1;
            Assert.Equal(expected, result);

            ThrowAndCatch(new ArgumentException());
            expected["argument"] = 2;
            Assert.Equal(expected, result);

            ThrowAndCatch(new JsonSerializationException());
            expected["json_serialization"] = 1;
            Assert.Equal(expected, result);

            ThrowAndCatch(new ArgumentException());
            ThrowAndCatch(new ArgumentException());
            ThrowAndCatch(new ArgumentException());
            expected["argument"] = 5;
            Assert.Equal(expected, result);

            ThrowAndCatch(new DivideByZeroException());
            expected["other"] = 1;
            Assert.Equal(expected, result);
        }

        [Fact]
        public void IgnoresExceptions()
        {
            /* setup */
            Dictionary<string, long> result = MockCounter();
            Dictionary<string, long> expected = new Dictionary<string, long>();

            Dictionary<Type, string> recognizedExceptions = new Dictionary<Type, string>
            {
                { typeof(JsonSerializationException), "json_serialization" },
                { typeof(ArgumentException), "argument" },
            };

            HashSet<Type> ignoredExeptions = new HashSet<Type>
            {
                typeof(TaskCanceledException),
                typeof(OperationCanceledException),
            };
            new ExceptionCounter(recognizedExceptions, ignoredExeptions);

            /* test */
            Assert.Equal(expected, result);

            ThrowAndCatch(new ArgumentException());
            expected["argument"] = 1;
            Assert.Equal(expected, result);

            ThrowAndCatch(new TaskCanceledException());
            Assert.Equal(expected, result);

            ThrowAndCatch(new TaskCanceledException());
            ThrowAndCatch(new OperationCanceledException());
            Assert.Equal(expected, result);
        }

        [Fact]
        public async Task CountsExceptionsAsync()
        {
            /* setup */
            Dictionary<string, long> result = MockCounter();
            Dictionary<string, long> expected = new Dictionary<string, long>();

            Dictionary<Type, string> recognizedExceptions = new Dictionary<Type, string>
            {
                { typeof(JsonSerializationException), "json_serialization" },
                { typeof(ArgumentException), "argument" },
            };

            HashSet<Type> ignoredExeptions = new HashSet<Type>
            {
                typeof(TaskCanceledException),
                typeof(OperationCanceledException),
            };
            new ExceptionCounter(recognizedExceptions, ignoredExeptions);

            /* test */
            Assert.Equal(expected, result);

            await Task.Run(() => { ThrowAndCatch(new ArgumentException()); });
            expected["argument"] = 1;
            Assert.Equal(expected, result);

            await Task.Run(() => { ThrowAndCatch(new ArgumentException()); });
            expected["argument"] = 2;
            Assert.Equal(expected, result);

            await Task.Run(() => { ThrowAndCatch(new JsonSerializationException()); });
            expected["json_serialization"] = 1;
            Assert.Equal(expected, result);

            await Task.WhenAll(
                Task.Run(() => { ThrowAndCatch(new ArgumentException()); }),
                Task.Run(() => { ThrowAndCatch(new ArgumentException()); }),
                Task.Run(() => { ThrowAndCatch(new ArgumentException()); })
            );
            expected["argument"] = 5;
            Assert.Equal(expected, result);

            await Task.Run(() => { ThrowAndCatch(new DivideByZeroException()); });
            expected["other"] = 1;
            Assert.Equal(expected, result);
        }

        private Dictionary<string, long> MockCounter()
        {
            Dictionary<string, long> result = new Dictionary<string, long>();
            void Increment(long val, string[] tags)
            {
                if (result.ContainsKey(tags[0]))
                {
                    result[tags[0]]++;
                }
                else
                {
                    result[tags[0]] = 1;
                }
            }
            var metricsProvider = new Mock<IMetricsProvider>();

            var counter = new Mock<IMetricsCounter>();
            counter.Setup(x => x.Increment(It.IsAny<long>(), It.IsAny<string[]>())).Callback((Action<long, string[]>)Increment);
            metricsProvider.Setup(x => x.CreateCounter(
                    "exceptions_total",
                    It.IsAny<string>(),
                    new List<string> { "exception_name" }))
                .Returns(counter.Object);

            Util.Metrics.Metrics.Init(metricsProvider.Object, new NullMetricsListener(), NullLogger.Instance);

            return result;
        }

        private void ThrowAndCatch(Exception e)
        {
            try
            {
                throw e;
            }
            catch { }
        }
    }
}
