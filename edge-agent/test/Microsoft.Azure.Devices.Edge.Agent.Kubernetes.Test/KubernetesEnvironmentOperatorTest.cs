// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.Test
{
    using System;
    using System.Threading;
    using k8s;
    using k8s.Models;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;

    public class KubernetesEnvironmentOperatorTest
    {
        const string DeviceNamespace = "a-namespace";
        const string ExceptionMessage = "ExceptionMessage";

        [Unit]
        [Fact]
        public void CtsIsCancelledOnRestartFailure()
        {
            Option<CancellationTokenSource> cts = Option.Some(new CancellationTokenSource());
            Exception listingError = new Exception(ExceptionMessage);

            var runtimeInfo = Mock.Of<IRuntimeInfoSource>();

            var client = Mock.Of<IKubernetes>();
            Mock.Get(client).Setup(c => c.ListNamespacedPodWithHttpMessagesAsync(DeviceNamespace, null, null, null, null, null, null, It.IsAny<int?>(), true, null, null, It.IsAny<CancellationToken>()))
                .Throws(listingError);

            var edgeOperator = new KubernetesEnvironmentOperator(
                DeviceNamespace,
                runtimeInfo,
                client,
                180);

            Assert.Throws<Exception>(() => edgeOperator.RestartWatch(cts));
            Assert.True(cts.OrDefault().IsCancellationRequested);
            Mock.Get(runtimeInfo).VerifyAll();
            Mock.Get(client).VerifyAll();
        }

        [Unit]
        [Fact]
        public void CtsIsCancelledOnError()
        {
            Option<CancellationTokenSource> cts = Option.Some(new CancellationTokenSource());
            Exception controllerException = new Exception(ExceptionMessage);

            var runtimeInfo = Mock.Of<IRuntimeInfoSource>();

            var client = Mock.Of<IKubernetes>();

            var edgeOperator = new KubernetesEnvironmentOperator(
                DeviceNamespace,
                runtimeInfo,
                client,
                180);

            Assert.Throws<Exception>(() => edgeOperator.HandleError(controllerException, cts));
            Assert.True(cts.OrDefault().IsCancellationRequested);
            Mock.Get(runtimeInfo).VerifyAll();
            Mock.Get(client).VerifyAll();
        }
    }
}