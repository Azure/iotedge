// Copyright (c) Microsoft. All rights reserved.
namespace LeafDeviceTest
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;

    internal class LeafDevice : Details
    {
        LeafDevice(
            string iothubConnectionString,
            string eventhubCompatibleEndpointWithEntityPath,
            string deviceId,
            string trustedCACertificateFileName,
            string edgeHostName,
            string edgeDeviceId,
            DeviceProtocol protocol,
            Option<DeviceCertificate> deviceCertificate,
            Option<IList<string>> thumbprintCertificates)
            : base(
                iothubConnectionString,
                eventhubCompatibleEndpointWithEntityPath,
                deviceId,
                trustedCACertificateFileName,
                edgeHostName,
                edgeDeviceId,
                protocol,
                deviceCertificate,
                thumbprintCertificates)
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

        public class LeafDeviceBuilder
        {
            readonly string iothubConnectionString;
            readonly string eventhubCompatibleEndpointWithEntityPath;
            readonly string deviceId;
            readonly string trustedCACertificateFileName;
            readonly string edgeHostName;
            readonly string edgeDeviceId;
            readonly DeviceProtocol protocol;
            bool usePrimaryThumbprintClientCert;
            Option<string> x509CACertPath;
            Option<string> x509CAKeyPath;
            Option<IList<string>> thumbprintCerts;

            public LeafDeviceBuilder(
                string iothubConnectionString,
                string eventhubCompatibleEndpointWithEntityPath,
                string deviceId,
                string trustedCACertificateFileName,
                string edgeHostName,
                string edgeDeviceId,
                DeviceProtocol protocol)
            {
                this.iothubConnectionString = Preconditions.CheckNotNull(iothubConnectionString);
                this.eventhubCompatibleEndpointWithEntityPath = Preconditions.CheckNotNull(eventhubCompatibleEndpointWithEntityPath);
                this.deviceId = Preconditions.CheckNotNull(deviceId);
                this.trustedCACertificateFileName = Preconditions.CheckNotNull(trustedCACertificateFileName);
                this.edgeHostName = Preconditions.CheckNotNull(edgeHostName);
                this.edgeDeviceId = Preconditions.CheckNotNull(edgeDeviceId);
                this.protocol = protocol;
                this.usePrimaryThumbprintClientCert = false;
            }

            public LeafDeviceBuilder SetX509CAAuthProperties(string clientCertificatePath, string clientCertificateKeyPath)
            {
                this.x509CACertPath = Option.Some(Preconditions.CheckNotNull(clientCertificatePath));
                this.x509CAKeyPath = Option.Some(Preconditions.CheckNotNull(clientCertificateKeyPath));
                this.thumbprintCerts = Option.None<IList<string>>();
                return this;
            }

            public LeafDeviceBuilder SetX509ThumbprintAuthProperties(
                string primaryClientCertificatePath,
                string primaryClientCertificateKeyPath,
                string secondaryClientCertificatePath,
                string secondaryClientCertificateKeyPath,
                bool usePrimaryForAuthentication)
            {
                this.usePrimaryThumbprintClientCert = usePrimaryForAuthentication;
                IList<string> thumbprintCerts = new List<string>();
                if (this.usePrimaryThumbprintClientCert)
                {
                    this.x509CACertPath = Option.Some(Preconditions.CheckNotNull(primaryClientCertificatePath));
                    this.x509CAKeyPath = Option.Some(Preconditions.CheckNotNull(primaryClientCertificateKeyPath));
                    thumbprintCerts.Add(primaryClientCertificatePath);
                    thumbprintCerts.Add(Preconditions.CheckNotNull(secondaryClientCertificatePath));
                }
                else
                {
                    this.x509CACertPath = Option.Some(Preconditions.CheckNotNull(secondaryClientCertificatePath));
                    this.x509CAKeyPath = Option.Some(Preconditions.CheckNotNull(secondaryClientCertificateKeyPath));
                    thumbprintCerts.Add(Preconditions.CheckNotNull(primaryClientCertificatePath));
                    thumbprintCerts.Add(secondaryClientCertificatePath);
                }

                this.thumbprintCerts = Option.Some(thumbprintCerts);
                return this;
            }

            public LeafDevice Build()
            {
                Option<DeviceCertificate> deviceCert = this.x509CACertPath.Map(
                    cert =>
                    {
                        return new DeviceCertificate
                        {
                            CertificateFilePath = cert,
                            PrivateKeyFilePath = this.x509CAKeyPath.Expect(() => new InvalidOperationException("Expected key file path"))
                        };
                    });
                return new LeafDevice(
                    this.iothubConnectionString,
                    this.eventhubCompatibleEndpointWithEntityPath,
                    this.deviceId,
                    this.trustedCACertificateFileName,
                    this.edgeHostName,
                    this.edgeDeviceId,
                    this.protocol,
                    deviceCert,
                    this.thumbprintCerts);
            }
        }
    }
}
