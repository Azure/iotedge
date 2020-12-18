// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test
{
    using System;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Test.Common;
    using Microsoft.Azure.Devices.Edge.Test.Common.Config;
    using Microsoft.Azure.Devices.Edge.Test.Helpers;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common.NUnit;
    using NUnit.Framework;

    [EndToEnd]
    class DeviceWithCustomCertificates : CustomCertificatesFixture
    {
        [Test]
        public async Task TransparentGateway()//@TO DO put back original test
        {
            CancellationToken token = this.TestToken;

Protocol protocol = Protocol.Mqtt;
TestAuthenticationType testAuth = TestAuthenticationType.CertificateAuthority;

            await this.runtime.DeployConfigurationAsync(token, Context.Current.NestedEdge);

            string leafDeviceId = DeviceId.Current.Generate();

            Option<string> parentId = testAuth == TestAuthenticationType.SasOutOfScope
                ? Option.None<string>()
                : Option.Some(this.runtime.DeviceId);
            
            var leaf = await LeafDevice.CreateAsync(
                leafDeviceId,
                protocol,
                testAuth.ToAuthenticationType(),
                parentId,
                testAuth.UseSecondaryCertificate(),
                this.ca,
                this.iotHub,
                Context.Current.Hostname.GetOrElse(Dns.GetHostName().ToLower()),
                token,
                Option.None<string>());

            await TryFinally.DoAsync(
                async () =>
                {
                    DateTime seekTime = DateTime.Now;
                    await leaf.SendEventAsync(token);
                    await leaf.WaitForEventsReceivedAsync(seekTime, token);
                    await leaf.InvokeDirectMethodAsync(token);
                },
                async () =>
                {
                    await leaf.DeleteIdentityAsync(token);
                });
        }
    }
}
