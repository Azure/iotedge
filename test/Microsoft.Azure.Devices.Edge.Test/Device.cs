// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Test.Common;
    using Microsoft.Azure.Devices.Edge.Test.Common.Certs;
    using Microsoft.Azure.Devices.Edge.Test.Common.Config;
    using Microsoft.Azure.Devices.Edge.Test.Helpers;
    using Microsoft.Azure.Devices.Edge.Util;
    using NUnit.Framework;

    class Device : RuntimeFixture
    {
        [Test]
        public async Task QuickstartCerts()
        {
            CancellationToken token = this.cts.Token;

            await this.runtime.DeployConfigurationAsync(token);

            string leafDeviceId = $"{Context.Current.DeviceId}-quickstart-certs";

            var leaf = await LeafDevice.CreateAsync(
                leafDeviceId,
                Protocol.Amqp,
                AuthenticationType.Sas,
                Option.None<string>(),
                false,
                CertificateAuthority.GetQuickstart(),
                this.iotHub,
                token);

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
