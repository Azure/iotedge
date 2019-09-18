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
            CertificateAuthority ca,
            IotHub iotHub,
            CancellationToken token)
        {
            return Profiler.Run(
                async () =>
                {
                    ITransportSettings transport = protocol.ToTransportSettings();
                    OsPlatform.Current.InstallEdgeCertificates(ca.Certificates.TrustedCertificates, transport);

                    string edgeHostname = Dns.GetHostName().ToLower();

                    switch (auth)
                    {
                        case AuthenticationType.Sas:
                            return await CreateWithSasAsync(
                                leafDeviceId,
                                parentId,
                                iotHub,
                                transport,
                                edgeHostname,
                                token);

                        case AuthenticationType.CertificateAuthority:
                            {
                                string p = parentId.Expect(() => new ArgumentException("Missing parent ID"));
                                return await CreateWithCaCertAsync(
                                    leafDeviceId,
                                    p,
                                    ca,
                                    iotHub,
                                    transport,
                                    edgeHostname,
                                    token);
                            }

                        case AuthenticationType.SelfSigned:
                            {
                                string p = parentId.Expect(() => new ArgumentException("Missing parent ID"));
                                return await CreateWithSelfSignedCertAsync(
                                    leafDeviceId,
                                    p,
                                    useSecondaryCertificate,
                                    ca,
                                    iotHub,
                                    transport,
                                    edgeHostname,
                                    token);
                            }

                        default:
                            throw new InvalidEnumArgumentException();
                    }
                },
                "Created leaf device '{Device}' on hub '{IotHub}'",
                leafDeviceId,
                iotHub.Hostname);
        }

        static async Task<LeafDevice> CreateWithSasAsync(
            string leafDeviceId,
            Option<string> parentId,
            IotHub iotHub,
            ITransportSettings transport,
            string edgeHostname,
            CancellationToken token)
        {
            Device leaf = new Device(leafDeviceId)
            {
                Authentication = new AuthenticationMechanism
                {
                    Type = AuthenticationType.Sas
                }
            };

            await parentId.ForEachAsync(
                async p =>
                {
                    Device edge = await GetEdgeDeviceIdentityAsync(p, iotHub, token);
                    leaf.Scope = edge.Scope;
                });

            leaf = await iotHub.CreateDeviceIdentityAsync(leaf, token);

            return await DeleteIdentityIfFailedAsync(
                leaf,
                iotHub,
                token,
                () =>
                {
                    string connectionString =
                        $"HostName={iotHub.Hostname};" +
                        $"DeviceId={leaf.Id};" +
                        $"SharedAccessKey={leaf.Authentication.SymmetricKey.PrimaryKey};" +
                        $"GatewayHostName={edgeHostname}";

                    return CreateLeafDeviceAsync(
                        leaf,
                        () => DeviceClient.CreateFromConnectionString(connectionString, new[] { transport }),
                        iotHub,
                        token);
                });
        }

        static async Task<LeafDevice> CreateWithCaCertAsync(
            string leafDeviceId,
            string parentId,
            CertificateAuthority ca,
            IotHub iotHub,
            ITransportSettings transport,
            string edgeHostname,
            CancellationToken token)
        {
            Device edge = await GetEdgeDeviceIdentityAsync(parentId, iotHub, token);

            Device leaf = new Device(leafDeviceId)
            {
                Authentication = new AuthenticationMechanism
                {
                    Type = AuthenticationType.CertificateAuthority
                },
                Scope = edge.Scope
            };

            leaf = await iotHub.CreateDeviceIdentityAsync(leaf, token);

            return await DeleteIdentityIfFailedAsync(
                leaf,
                iotHub,
                token,
                async () =>
                {
                    LeafCertificates certFiles = await ca.GenerateLeafCertificatesAsync(leafDeviceId, token);

                    (X509Certificate2 leafCert, IEnumerable<X509Certificate2> trustedCerts) =
                        CertificateHelper.GetServerCertificateAndChainFromFile(certFiles.CertificatePath, certFiles.KeyPath);
                    // .NET runtime requires that we install the chain of CA certs, otherwise it can't
                    // provide them to a server during authentication.
                    OsPlatform.Current.InstallTrustedCertificates(trustedCerts);

                    return await CreateLeafDeviceAsync(
                        leaf,
                        () => DeviceClient.Create(
                            iotHub.Hostname,
                            edgeHostname,
                            new DeviceAuthenticationWithX509Certificate(leaf.Id, leafCert),
                            new[] { transport }),
                        iotHub,
                        token);
                });
        }

        static async Task<LeafDevice> CreateWithSelfSignedCertAsync(
            string leafDeviceId,
            string parentId,
            bool useSecondaryCertificate,
            CertificateAuthority ca,
            IotHub iotHub,
            ITransportSettings transport,
            string edgeHostname,
            CancellationToken token)
        {
            LeafCertificates primary = await ca.GenerateLeafCertificatesAsync(
                IdentityLimits.CheckCommonName($"{leafDeviceId}-1"),
                token);
            LeafCertificates secondary = await ca.GenerateLeafCertificatesAsync(
                IdentityLimits.CheckCommonName($"{leafDeviceId}-2"),
                token);

            string[] streams = await Task.WhenAll(
                new[]
                {
                    primary.CertificatePath,
                    secondary.CertificatePath
                }.Select(
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

            Device edge = await GetEdgeDeviceIdentityAsync(parentId, iotHub, token);

            Device leaf = new Device(leafDeviceId)
            {
                Authentication = new AuthenticationMechanism
                {
                    Type = AuthenticationType.SelfSigned,
                    X509Thumbprint = new X509Thumbprint
                    {
                        PrimaryThumbprint = thumbprints.First(),
                        SecondaryThumbprint = thumbprints.Last()
                    }
                },
                Scope = edge.Scope
            };

            leaf = await iotHub.CreateDeviceIdentityAsync(leaf, token);

            return await DeleteIdentityIfFailedAsync(
                leaf,
                iotHub,
                token,
                () =>
                {
                    LeafCertificates certFiles = useSecondaryCertificate ? secondary : primary;

                    (X509Certificate2 leafCert, _) =
                        CertificateHelper.GetServerCertificateAndChainFromFile(certFiles.CertificatePath, certFiles.KeyPath);

                    return CreateLeafDeviceAsync(
                        leaf,
                        () => DeviceClient.Create(
                            iotHub.Hostname,
                            edgeHostname,
                            new DeviceAuthenticationWithX509Certificate(leaf.Id, leafCert),
                            new[] { transport }),
                        iotHub,
                        token);
                });
        }

        static async Task<Device> GetEdgeDeviceIdentityAsync(string parentId, IotHub iotHub, CancellationToken token)
        {
            Device edge = await iotHub.GetDeviceIdentityAsync(parentId, token);
            if (edge == null)
            {
                throw new InvalidOperationException($"Device '{parentId}' not found in '{iotHub.Hostname}'");
            }

            return edge;
        }

        static async Task<LeafDevice> DeleteIdentityIfFailedAsync(Device device, IotHub iotHub, CancellationToken token, Func<Task<LeafDevice>> what)
        {
            try
            {
                return await what();
            }
            catch
            {
                await DeleteIdentityAsync(device, iotHub, token);
                throw;
            }
        }

        static async Task<LeafDevice> CreateLeafDeviceAsync(Device device, Func<DeviceClient> clientFactory, IotHub iotHub, CancellationToken token)
        {
            DeviceClient client = clientFactory();
            await client.SetMethodHandlerAsync(nameof(DirectMethod), DirectMethod, null, token);
            return new LeafDevice(device, client, iotHub);
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

                        Log.Verbose($"Received event for '{devId}' with message ID '{msgId}' and body '{Encoding.UTF8.GetString(data.Body)}'");

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
                    new CloudToDeviceMethod(nameof(DirectMethod)),
                    token),
                "Invoked method on leaf device from the cloud");

        public Task DeleteIdentityAsync(CancellationToken token) =>
            DeleteIdentityAsync(this.device, this.iotHub, token);

        static Task DeleteIdentityAsync(Device device, IotHub iotHub, CancellationToken token) =>
            Profiler.Run(
                () => iotHub.DeleteDeviceIdentityAsync(device, token),
                "Deleted leaf device '{Device}'",
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
