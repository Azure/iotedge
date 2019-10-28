// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core.Test.Metrics
{
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

    [Unit]
    public class ExceptionCounterTest
    {
        [Fact]
        public void CountsExceptions()
        {
            /* setup */
            Dictionary<string, long> result = this.MockCounter();
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

            using (new ExceptionCounter(recognizedExceptions, ignoredExeptions))
            {
                /* test */
                Assert.Equal(expected, result);

                this.ThrowAndCatch(new ArgumentException());
                expected["argument"] = 1;
                Assert.Equal(expected, result);

                this.ThrowAndCatch(new ArgumentException());
                expected["argument"] = 2;
                Assert.Equal(expected, result);

                this.ThrowAndCatch(new JsonSerializationException());
                expected["json_serialization"] = 1;
                Assert.Equal(expected, result);

                this.ThrowAndCatch(new ArgumentException());
                this.ThrowAndCatch(new ArgumentException());
                this.ThrowAndCatch(new ArgumentException());
                expected["argument"] = 5;
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
            Dictionary<string, long> result = this.MockCounter();
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
            using (new ExceptionCounter(recognizedExceptions, ignoredExeptions))
            {
                /* test */
                Assert.Equal(expected, result);

                this.ThrowAndCatch(new ArgumentException());
                expected["argument"] = 1;
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
            Dictionary<string, long> result = this.MockCounter();

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
            using (new ExceptionCounter(recognizedExceptions, ignoredExeptions))
            {
                /* test */
                await Task.Run(() => { this.ThrowAndCatch(new ArgumentException()); });
                Assert.Equal(1, result["argument"]);

                await Task.Run(() => { this.ThrowAndCatch(new ArgumentException()); });
                Assert.Equal(2, result["argument"]);

                await Task.Run(() => { this.ThrowAndCatch(new JsonSerializationException()); });
                Assert.Equal(1, result["json_serialization"]);

                await Task.WhenAll(
                    Task.Run(() => { this.ThrowAndCatch(new ArgumentException()); }),
                    Task.Run(() => { this.ThrowAndCatch(new ArgumentException()); }),
                    Task.Run(() => { this.ThrowAndCatch(new ArgumentException()); }));
                Assert.Equal(5, result["argument"]);
            }
        }

        [Fact]
        public void IgnoresExceptions()
        {
            /* setup */
            Dictionary<string, long> result = this.MockCounter();
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
            using (new ExceptionCounter(recognizedExceptions, ignoredExeptions))
            {
                /* test */
                Assert.Equal(expected, result);

                this.ThrowAndCatch(new ArgumentException());
                expected["argument"] = 1;
                Assert.Equal(expected, result);

                this.ThrowAndCatch(new TaskCanceledException());
                Assert.Equal(expected, result);

                this.ThrowAndCatch(new TaskCanceledException());
                this.ThrowAndCatch(new OperationCanceledException());
                Assert.Equal(e