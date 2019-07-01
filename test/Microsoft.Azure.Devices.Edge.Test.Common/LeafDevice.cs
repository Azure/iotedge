// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common
{
    using System;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Test.Common.Certs;
    using Microsoft.Azure.Devices.Edge.Test.Common.Config;
    using Microsoft.Azure.Devices.Edge.Util;
    using Serilog;

    public enum AuthType
    {
        Sas,
        X509Certificate,
        X509Thumbprint
    }

    public class LeafDevice
    {
        readonly AuthType auth;
        readonly DeviceClient client;
        readonly Device device;
        readonly EdgeCertificateAuthority edgeCa;
        readonly IotHub iotHub;
        readonly string messageId;
        readonly Option<string> scope;

        LeafDevice(Device device, DeviceClient client, AuthType auth, Option<string> scope, EdgeCertificateAuthority edgeCa, IotHub iotHub)
        {
            this.auth = auth;
            this.client = client;
            this.device = device;
            this.edgeCa = edgeCa;
            this.iotHub = iotHub;
            this.messageId = Guid.NewGuid().ToString();
            this.scope = scope;
        }

        public static Task<LeafDevice> CreateAsync(
            string leafDeviceId,
            Protocol protocol,
            AuthType auth,
            Option<string> scope,
            EdgeCertificateAuthority edgeCa,
            IotHub iotHub,
            CancellationToken token)
        {
            return Profiler.Run(
                async () =>
                {
                    ITransportSettings transport = protocol.ToTransportSettings();
                    Platform.InstallTrustedCertificates(edgeCa.Certificates.TrustedCertificates, transport);

                    Device leaf = await iotHub.CreateLeafDeviceIdentityAsync(leafDeviceId, token);

                    string connectionString =
                        $"HostName={iotHub.Hostname};" +
                        $"DeviceId={leaf.Id};" +
                        $"SharedAccessKey={leaf.Authentication.SymmetricKey.PrimaryKey};" +
                        $"GatewayHostName={Dns.GetHostName()}";

                    var client = DeviceClient.CreateFromConnectionString(connectionString, new[] { transport });
                    await client.SetMethodHandlerAsync("DirectMethod", DirectMethod, null, token);

                    return new LeafDevice(leaf, client, auth, scope, edgeCa, iotHub);
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

        public Task WaitForEventsReceivedAsync(CancellationToken token)
        {
            return Profiler.Run(
                () => this.iotHub.ReceiveEventsAsync(
                    this.device.Id,
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
            Profiler.Run(
                () => this.iotHub.DeleteDeviceIdentityAsync(this.device, token),
                "Deleted device '{Device}'",
                this.device.Id);

        static Task<MethodResponse> DirectMethod(MethodRequest request, object context)
        {
            Log.Verbose(
                "Leaf device received direct method call with payload: {Payload}",
                request.DataAsJson);
            return Task.FromResult(new MethodResponse(request.Data, (int)HttpStatusCode.OK));
        }
    }
}
