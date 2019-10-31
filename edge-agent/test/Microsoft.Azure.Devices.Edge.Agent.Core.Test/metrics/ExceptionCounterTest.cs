// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core.Test.Metrics
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using System.Runtime.CompilerServices;
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

    [Unit]
    public class ExceptionCounterTest
    {
        [Fact]
        public void CountsExceptions()
        {
            /* setup */
            (Dictionary<string, long> result, IMetricsProvider provider) = this.MockCounter();
            Dictionary<string, long> expected = new Dictionary<string, long>();

            Dictionary<Type, string> recognizedExceptions = new Dictionary<Type, string>
            {
                { typeof(TestException1), "test1" },
                { typeof(TestException2), "test2" },
            };
            using (new ExceptionCounter(recognizedExceptions, new HashSet<Type>(), provider))
            {
                /* test */
                Assert.Equal(expected, result);

                this.ThrowAndCatch(new TestException2());
                expected["test2"] = 1;
                Assert.Equal(expected, result);

                this.ThrowAndCatch(new TestException2());
                expected["test2"] = 2;
                Assert.Equal(expected, result);

                this.ThrowAndCatch(new TestException1());
                expected["test1"] = 1;
                Assert.Equal(expected, result);

                this.ThrowAndCatch(new TestException2());
                this.ThrowAndCatch(new TestException2());
                this.ThrowAndCatch(new TestException2());
                expected["test2"] = 5;
                Assert.Equal(expected, result);

                this.ThrowAndCatch(new DivideByZeroException());
                expected["other"] = 1;
                Assert.Equal(expected, result);
            }
        }

        [Fact]
        public void IgnoresExceptions()
        {
            /* setup */
            (Dictionary<string, long> result, IMetricsProvider provider) = this.MockCounter();
            Dictionary<string, long> expected = new Dictionary<string, long>();

            Dictionary<Type, string> recognizedExceptions = new Dictionary<Type, string>
            {
                { typeof(TestException1), "test1" },
                { typeof(TestException2), "test2" },
            };
            HashSet<Type> ignoredExceptions = new HashSet<Type>
            {
                typeof(TaskCanceledException),
                typeof(OperationCanceledException),
            };
            using (new ExceptionCounter(recognizedExceptions, ignoredExceptions, provider))
            {
                /* test */
                Assert.Equal(expected, result);

                this.ThrowAndCatch(new TestException2());
                expected["test2"] = 1;
                Assert.Equal(expected, result);

                this.ThrowAndCatch(new TaskCanceledException());
                Assert.Equal(expected, result);

                this.ThrowAndCatch(new TaskCanceledException());
                this.ThrowAndCatch(new OperationCanceledException());
                Assert.Equal(expected, result);
            }
        }

        [Fact]
        public async Task CountsExceptionsAsync()
        {
            /* setup */
            (Dictionary<string, long> result, IMetricsProvider provider) = this.MockCounter();

            Dictionary<Type, string> recognizedExceptions = new Dictionary<Type, string>
            {
                { typeof(TestException1), "test1" },
                { typeof(TestException2), "test2" },
            };
            using (new ExceptionCounter(recognizedExceptions, new HashSet<Type>(), provider))
            {
                /* test */
                await Task.Run(() => { this.ThrowAndCatch(new TestException2()); });
                Assert.Equal(1, result["test2"]);

                await Task.Run(() => { this.ThrowAndCatch(new TestException1()); });
                Assert.Equal(1, result["test1"]);

                await Task.Run(() => { this.ThrowAndCatch(new TestException2()); });
                Assert.Equal(2, result["test2"]);

                await Task.WhenAll(
                    Task.Run(() => { this.ThrowAndCatch(new TestException2()); }),
                    Task.Run(() => { this.ThrowAndCatch(new TestException2()); }),
                    Task.Run(() => { this.ThrowAndCatch(new TestException2()); }));
                Assert.Equal(5, result["test2"]);
            }
        }

        [Fact]
        public void DisposeUnsubscribes()
        {
            /* setup */
            (Dictionary<string, long> result, IMetricsProvider provider) = this.MockCounter();
            Dictionary<string, long> expected = new Dictionary<string, long>();
            using (new ExceptionCounter(new Dictionary<Type, string>(), new HashSet<Type>(), provider))
            {
                this.ThrowAndCatch(new Exception());
                expected["other"] = 1;
                Assert.Equal(expected, result);
            }

            this.ThrowAndCatch(new Exception());
            Assert.Equal(expected, result);
        }

        (Dictionary<string, long> result, IMetricsProvider provider) MockCounter()
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

            return (result, metricsProvider.Object);
        }

        void ThrowAndCatch(Exception e)
        {
            try
            {
                throw e;
            }
            catch
            {
            }
        }

        class TestException1 : Exception
        {
        }

        class TestException2 : Exception
        {
        }
    }
}
