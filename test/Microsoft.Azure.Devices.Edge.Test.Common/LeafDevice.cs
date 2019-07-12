// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Test.Common.Certs;
    using Microsoft.Azure.Devices.Edge.Test.Common.Config;
    using Microsoft.Azure.Devices.Edge.Util;
    using Serilog;

    public class LeafDevice
    {
        readonly DeviceClient client;
        readonly Device device;
        readonly IotHub iotHub;
        readonly string messageId;

        LeafDevice(Device device, DeviceClient client, IotHub iotHub)
        {
            this.client = client;
            this.device = device;
            this.iotHub = iotHub;
            this.messageId = Guid.NewGuid().ToString();
        }

        public static Task<LeafDevice> CreateAsync(
            string leafDeviceId,
            Protocol protocol,
            AuthenticationType auth,
            Option<string> parentId,
            bool useSecondaryCertificate,
            EdgeCertificateAuthority edgeCa,
            IotHub iotHub,
            CancellationToken token)
        {
            return Profiler.Run(
                async () =>
                {
                    ITransportSettings transport = protocol.ToTransportSettings();
                    Platform.InstallEdgeCertificates(edgeCa.Certificates.TrustedCertificates, transport);

                    string edgeHostname = Dns.GetHostName().ToLower();

                    if (auth == AuthenticationType.Sas)
                    {
                        Device leaf = await iotHub.CreateLeafDeviceIdentityAsync(
                            leafDeviceId,
                            parentId,
                            auth,
                            Option.None<X509Thumbprint>(),
                            token);

                        try
                        {
                            string connectionString =
                                $"HostName={iotHub.Hostname};" +
                                $"DeviceId={leaf.Id};" +
                                $"SharedAccessKey={leaf.Authentication.SymmetricKey.PrimaryKey};" +
                                $"GatewayHostName={edgeHostname}";

                            DeviceClient client = DeviceClient.CreateFromConnectionString(connectionString, new[] { transport });

                            await client.SetMethodHandlerAsync("DirectMethod", DirectMethod, null, token);

                            return new LeafDevice(leaf, client, iotHub);
                        }
                        catch
                        {
                            await DeleteIdentityAsync(leaf, iotHub, token);
                            throw;
                        }
                    }
                    else if (auth == AuthenticationType.CertificateAuthority)
                    {
                        // TODO: Cert gen fails in openssl.exe if leaf deviceId > 64 chars
                        LeafCertificates certFiles = await edgeCa.GenerateLeafCertificatesAsync(leafDeviceId, token);

                        Device leaf = await iotHub.CreateLeafDeviceIdentityAsync(
                            leafDeviceId,
                            parentId,
                            auth,
                            Option.None<X509Thumbprint>(),
                            token);

                        try
                        {
                            (X509Certificate2 leafCert, IEnumerable<X509Certificate2> trustedCerts) =
                                CertificateHelper.GetServerCertificateAndChainFromFile(certFiles.CertificatePath, certFiles.KeyPath);
                            // .NET runtime requires that we install the chain of CA certs, otherwise it can't
                            // provide them to a server during authentication.
                            Platform.InstallTrustedCertificates(trustedCerts);

                            DeviceClient client = DeviceClient.Create(
                                iotHub.Hostname,
                                edgeHostname,
                                new DeviceAuthenticationWithX509Certificate(leaf.Id, leafCert),
                                new[] { transport });

                            await client.SetMethodHandlerAsync("DirectMethod", DirectMethod, null, token);

                            return new LeafDevice(leaf, client, iotHub);
                        }
                        catch
                        {
                            await DeleteIdentityAsync(leaf, iotHub, token);
                            throw;
                        }
                    }
                    else if (auth == AuthenticationType.SelfSigned)
                    {
                        // TODO: Cert gen fails in openssl.exe if leaf deviceId > 64 chars
                        LeafCertificates primary = await edgeCa.GenerateLeafCertificatesAsync($"{leafDeviceId}-pri", token);
                        LeafCertificates secondary = await edgeCa.GenerateLeafCertificatesAsync($"{leafDeviceId}-sec", token);
                        LeafCertificates certFiles = useSecondaryCertificate ? secondary : primary;

                        var paths = new List<string>
                        {
                            primary.CertificatePath,
                            secondary.CertificatePath
                        };

                        string[] streams = await Task.WhenAll(
                            paths.Select(
                                async p =>
                                {
                                    using (var sr = new StreamReader(p))
                                    {
                                        return await sr.ReadToEndAsync();
                                    }
                                }));

                        string[] thumbprints = CertificateHelper.GetCertificatesFromPem(streams)
                            .Select(c => c.Thumbprint?.ToUpper(CultureInfo.InvariantCulture))
                            .ToArray();

                        var x509Thumbprint = new X509Thumbprint
                        {
                            PrimaryThumbprint = thumbprints.First(),
                            SecondaryThumbprint = thumbprints.Last()
                        };

                        Device leaf = await iotHub.CreateLeafDeviceIdentityAsync(
                            leafDeviceId,
                            parentId,
                            auth,
                            Option.Some(x509Thumbprint),
                            token);

                        try
                        {
                            (X509Certificate2 leafCert, _) =
                                CertificateHelper.GetServerCertificateAndChainFromFile(certFiles.CertificatePath, certFiles.KeyPath);

                            DeviceClient client = DeviceClient.Create(
                                iotHub.Hostname,
                                edgeHostname,
                                new DeviceAuthenticationWithX509Certificate(leaf.Id, leafCert),
                                new[] { transport });

                            await client.SetMethodHandlerAsync("DirectMethod", DirectMethod, null, token);

                            return new LeafDevice(leaf, client, iotHub);
                        }
                        catch
                        {
                            await DeleteIdentityAsync(leaf, iotHub, token);
                            throw;
                        }
                    }
                    else
                    {
                        throw new InvalidEnumArgumentException();
                    }
                },
                "Created leaf device '{Device}' on hub '{IotHub}'",
                leafDeviceId,
                iotHub.Hostname);
        }

        public Task SendEventAsync(CancellationToken token)
        {
            var message = new Message(Encoding.ASCII.GetBytes(this.device.Id))
            {
                Properties = { ["leaf-message-id"] = this.messageId }
            };
            return this.client.SendEventAsync(message, token);
        }

        public Task WaitForEventsReceivedAsync(DateTime seekTime, CancellationToken token)
        {
            return Profiler.Run(
                () => this.iotHub.ReceiveEventsAsync(
                    this.device.Id,
                    seekTime,
                    data =>
                    {
                        data.SystemProperties.TryGetValue("iothub-connection-device-id", out object devId);
                        data.Properties.TryGetValue("leaf-message-id", out object msgId);

                        return devId != null && devId.ToString().Equals(this.device.Id)
                                             && msgId != null && msgId.ToString().Equals(this.messageId);
                    },
                    token),
                "Received events from device '{Device}' on Event Hub '{EventHub}'",
                this.device.Id,
                this.iotHub.EntityPath);
        }

        public Task InvokeDirectMethodAsync(CancellationToken token) =>
            Profiler.Run(
                () => this.iotHub.InvokeMethodAsync(
                    this.device.Id,
                    new CloudToDeviceMethod("DirectMethod"),
                    token),
                "Invoked method on leaf device from the cloud");

        // BUG: callers can continue to try to use this object after DeleteIdentityAsync has been called.
        public Task DeleteIdentityAsync(CancellationToken token) =>
            DeleteIdentityAsync(this.device, this.iotHub, token);

        static Task DeleteIdentityAsync(Device device, IotHub iotHub, CancellationToken token) =>
            Profiler.Run(
                () => iotHub.DeleteDeviceIdentityAsync(device, token),
                "Deleted device '{Device}'",
                device.Id);

        static Task<MethodResponse> DirectMethod(MethodRequest request, object context)
        {
            Log.Verbose(
                "Leaf device received direct method call with payload: {Payload}",
                request.DataAsJson);
            return Task.FromResult(new MethodResponse(request.Data, (int)HttpStatusCode.OK));
        }
    }
}
