// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Common.Exceptions;
    using Microsoft.Azure.Devices.Edge.Test.Common;
    using Microsoft.Azure.Devices.Edge.Test.Common.Certs;
    using Microsoft.Azure.Devices.Edge.Test.Common.Config;
    using Microsoft.Azure.Devices.Edge.Test.Helpers;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common.NUnit;
    using NUnit.Framework;

    [EndToEnd]
    class Device : SasManualProvisioningFixture
    {
        [Test]
        [Category("CentOsSafe")]
        public async Task QuickstartCerts()
        {
            CancellationToken token = this.TestToken;

            await this.runtime.DeployConfigurationAsync(token);

            string leafDeviceId = DeviceId.Current.Generate();

            var leaf = await LeafDevice.CreateAsync(
                leafDeviceId,
                Protocol.Amqp,
                AuthenticationType.Sas,
                Option.Some(this.runtime.DeviceId),
                false,
                CertificateAuthority.GetQuickstart(),
                this.iotHub,
                token,
                Option.None<string>());

            try
            {
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
            catch (IotHubException)
            {
                token = new CancellationTokenSource(TimeSpan.FromMinutes(6)).Token;
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
}
