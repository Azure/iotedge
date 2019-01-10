// Copyright (c) Microsoft. All rights reserved.
namespace LeafDevice
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using global::LeafDevice.Details;
    using Microsoft.Azure.Devices.Edge.Util;

    public class LeafDevice : Details.Details
    {
        public LeafDevice(
            string iothubConnectionString,
            string eventhubCompatibleEndpointWithEntityPath,
            string deviceId,
            string trustedCACertificateFileName,
            string edgeHostName,
            bool useWebSockets)
            :
            base(
                iothubConnectionString,
                eventhubCompatibleEndpointWithEntityPath,
                deviceId,
                trustedCACertificateFileName,
                edgeHostName,
                useWebSockets,
                Option.None<DeviceCertificate>(),
                Option.None<IList<string>>())
        {
        }

        public LeafDevice(
            string iothubConnectionString,
            string eventhubCompatibleEndpointWithEntityPath,
            string deviceId,
            string trustedCACertificateFileName,
            string edgeHostName,
            bool useWebSockets,
            string clientCertificatePath,
            string clientCertificateKeyPath
        )
            :
            base(
                iothubConnectionString,
                eventhubCompatibleEndpointWithEntityPath,
                deviceId,
                trustedCACertificateFileName,
                edgeHostName,
                useWebSockets,
                Option.Some(new DeviceCertificate { CertificateFilePath = clientCertificatePath, PrivateKeyFilePath = clientCertificateKeyPath }),
                Option.None<IList<string>>())
        {
        }

        public LeafDevice(
            string iothubConnectionString,
            string eventhubCompatibleEndpointWithEntityPath,
            string deviceId,
            string trustedCACertificateFileName,
            string edgeHostName,
            bool useWebSockets,
            string clientCertificatePath,
            string clientCertificateKeyPath,
            IList<string> thumprintCertificates
        )
            :
            base(
                iothubConnectionString,
                eventhubCompatibleEndpointWithEntityPath,
                deviceId,
                trustedCACertificateFileName,
                edgeHostName,
                useWebSockets,
                Option.Some(new DeviceCertificate { CertificateFilePath = clientCertificatePath, PrivateKeyFilePath = clientCertificateKeyPath }),
                Option.Some(thumprintCertificates))
        {
        }

        public async Task RunAsync()
        {
            // This test assumes that there is an edge deployment running as transparent gateway.
            try
            {
                await this.InitializeTrustedCertsAsync();
                await this.GetOrCreateDeviceIdentityAsync();
                await this.ConnectToEdgeAndSendDataAsync();
                await this.VerifyDataOnIoTHubAsync();
                await this.VerifyDirectMethodAsync();
            }
            catch (Exception)
            {
                Console.WriteLine("** Oops, there was a problem.");
                this.KeepDeviceIdentity();
                throw;
            }
            finally
            {
                // only remove the identity if we created it; if it already existed in IoT Hub then leave it alone
                await this.MaybeDeleteDeviceIdentity();
            }
        }
    }
}
