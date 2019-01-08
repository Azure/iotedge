// Copyright (c) Microsoft. All rights reserved.
// ReSharper disable ArrangeThisQualifier
namespace LeafDevice
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;

    public class LeafDevice : Details.Details
    {
        public LeafDevice(
            string iothubConnectionString,
            string eventhubCompatibleEndpointWithEntityPath,
            string deviceId,
            string trustedCACertificateFileName,
            string edgeHostName,
            bool useWebSockets) :
            base(iothubConnectionString,
                eventhubCompatibleEndpointWithEntityPath,
                deviceId,
                trustedCACertificateFileName,
                edgeHostName,
                useWebSockets,
                Option.None<Details.DeviceCertificate>(),
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
            ) :
            base(iothubConnectionString,
                eventhubCompatibleEndpointWithEntityPath,
                deviceId,
                trustedCACertificateFileName,
                edgeHostName,
                useWebSockets,
                Option.Some( new Details.DeviceCertificate { certificateFilePath = clientCertificatePath, certificateKeyFilePath = clientCertificateKeyPath } ),
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
            ) :
            base(iothubConnectionString,
                eventhubCompatibleEndpointWithEntityPath,
                deviceId,
                trustedCACertificateFileName,
                edgeHostName,
                useWebSockets,
                Option.Some(new Details.DeviceCertificate { certificateFilePath = clientCertificatePath, certificateKeyFilePath = clientCertificateKeyPath }),
                Option.Some(thumprintCertificates))
        {
        }
        
        public async Task RunAsync()
        {
            // This test assumes that there is an edge deployment running as transparent gateway.
            try
            {
                await this.InitializeServerCerts();
                await GetOrCreateDeviceIdentity();
                await ConnectToEdgeAndSendData();
                await this.VerifyDataOnIoTHub();
                await this.VerifyDirectMethod();
            }
            catch (Exception)
            {
                Console.WriteLine("** Oops, there was a problem.");
                KeepDeviceIdentity();
                throw;
            }
            finally
            {
                // only remove the identity if we created it; if it already existed in IoT Hub then leave it alone
                await MaybeDeleteDeviceIdentity();
            }
        }
    }
}
