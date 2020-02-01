// Copyright (c) Microsoft. All rights reserved.
namespace LeafDeviceTest
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Runtime.InteropServices;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Transport.Mqtt;
    using Microsoft.Azure.Devices.Common;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Azure.EventHubs;
    using DeviceClientTransportType = Microsoft.Azure.Devices.Client.TransportType;
    using EventHubClientTransportType = Microsoft.Azure.EventHubs.TransportType;
    using IotHubConnectionStringBuilder = Microsoft.Azure.Devices.IotHubConnectionStringBuilder;
    using Message = Microsoft.Azure.Devices.Client.Message;
    using ServiceClientTransportType = Microsoft.Azure.Devices.TransportType;

    public enum DeviceProtocol
    {
        Amqp,
        AmqpWS,
        Mqtt,
        MqttWs
    }

    public class Details
    {
        readonly string iothubConnectionString;
        readonly string eventhubCompatibleEndpointWithEntityPath;
        readonly string deviceId;
        readonly string trustedCACertificateFileName;
        readonly string edgeHostName;
        readonly Option<string> edgeDeviceId;
        readonly ServiceClientTransportType serviceClientTransportType;
        readonly EventHubClientTransportType eventHubClientTransportType;
        readonly ITransportSettings[] deviceTransportSettings;
        readonly AuthenticationType authType = AuthenticationType.None;
        readonly Option<X509Certificate2> clientCertificate;
        readonly Option<IEnumerable<X509Certificate2>> clientCertificateChain;
        readonly Option<List<string>> thumbprints;
        DeviceContext context;
        Option<IWebProxy> proxy;

        protected Details(
            string iothubConnectionString,
            string eventhubCompatibleEndpointWithEntityPath,
            string deviceId,
            string trustedCACertificateFileName,
            string edgeHostName,
            string edgeDeviceId,
            DeviceProtocol protocol,
            Option<string> proxy,
            Option<DeviceCertificate> clientCertificatePaths,
            Option<IList<string>> thumbprintCertificatePaths)
        {
            this.iothubConnectionString = iothubConnectionString;
            this.eventhubCompatibleEndpointWithEntityPath = eventhubCompatibleEndpointWithEntityPath;
            this.deviceId = deviceId;
            this.trustedCACertificateFileName = trustedCACertificateFileName;
            this.proxy = proxy.Map(p => new WebProxy(p) as IWebProxy);
            this.edgeHostName = edgeHostName;
            if (!edgeDeviceId.IsNullOrWhiteSpace())
            {
                this.edgeDeviceId = Option.Some(edgeDeviceId);
            }

            (this.authType,
                this.clientCertificate,
                this.clientCertificateChain,
                this.thumbprints) = ObtainAuthDetails(clientCertificatePaths, thumbprintCertificatePaths);

            if (protocol == DeviceProtocol.AmqpWS || protocol == DeviceProtocol.MqttWs)
            {
                this.serviceClientTransportType = ServiceClientTransportType.Amqp_WebSocket_Only;
                this.eventHubClientTransportType = EventHubClientTransportType.AmqpWebSockets;
                if (protocol == DeviceProtocol.MqttWs)
                {
                    this.deviceTransportSettings = new ITransportSettings[] { new MqttTransportSettings(DeviceClientTransportType.Mqtt_WebSocket_Only) };
                }
                else
                {
                    this.deviceTransportSettings = new ITransportSettings[] { new AmqpTransportSettings(DeviceClientTransportType.Amqp_WebSocket_Only) };
                }
            }
            else
            {
                this.serviceClientTransportType = ServiceClientTransportType.Amqp;
                this.eventHubClientTransportType = this.proxy.HasValue ? EventHubClientTransportType.AmqpWebSockets : EventHubClientTransportType.Amqp;
                if (protocol == DeviceProtocol.Mqtt)
                {
                    this.deviceTransportSettings = new ITransportSettings[] { new MqttTransportSettings(DeviceClientTransportType.Mqtt_Tcp_Only) };
                }
                else
                {
                    this.deviceTransportSettings = new ITransportSettings[] { new AmqpTransportSettings(DeviceClientTransportType.Amqp_Tcp_Only) };
                }
            }

            Console.WriteLine(
                $"Leaf Device Client: \n"
                + $"\t[authType={this.authType}] \n"
                + $"\t[clientCertificate subject name={this.clientCertificate.Match(c => c.SubjectName.ToString(), () => string.Empty)}] \n"
                + $"\t[clientCertificateChain count={this.clientCertificateChain.Match(c => c.Count(), () => 0)}] \n"
                + $"\t[service client transport type={this.serviceClientTransportType}]\n"
                + $"\t[event hub client transport type={this.eventHubClientTransportType}]\n"
                + $"\t[device transport type={this.deviceTransportSettings.First().GetTransportType()}]");
        }

        protected Task InitializeTrustedCertsAsync()
        {
            if (!string.IsNullOrEmpty(this.trustedCACertificateFileName))
            {
                // Since Windows will pop up security warning when add certificate to current user store location;
                // Therefore we will use CustomCertificateValidator instead.
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    Console.WriteLine("Hook up callback on device transport settings to validate with given certificate");
                    CustomCertificateValidator.Create(new List<X509Certificate2> { this.GetTrustedCertificate() }, this.deviceTransportSettings);
                }
                else
                {
                    Console.WriteLine("Install trusted CA certificates");
                    InstallTrustedCACerts(new List<X509Certificate2> { this.GetTrustedCertificate() });
                }
            }

            // for dotnet runtime, in order to provide the entire client certificate chain when
            // authenticating with a server it is required that these chain CA certificates
            // are installed as trusted CAs.
            this.clientCertificateChain.ForEach(certs => InstallTrustedCACerts(certs));
            return Task.CompletedTask;
        }

        protected async Task ConnectToEdgeAndSendDataAsync()
        {
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(600))) // Long timeout is needed because registry manager takes a while for the device identity to be usable
            {
                Exception savedException = null;

                try
                {
                    var builder = IotHubConnectionStringBuilder.Create(this.iothubConnectionString);
                    DeviceClient deviceClient;
                    if (this.authType == AuthenticationType.Sas)
                    {
                        string leafDeviceConnectionString = $"HostName={builder.HostName};DeviceId={this.deviceId};SharedAccessKey={this.context.Device.Authentication.SymmetricKey.PrimaryKey};GatewayHostName={this.edgeHostName}";
                        deviceClient = DeviceClient.CreateFromConnectionString(leafDeviceConnectionString, this.deviceTransportSettings);
                    }
                    else
                    {
                        var auth = new DeviceAuthenticationWithX509Certificate(this.deviceId, this.clientCertificate.Expect(() => new InvalidOperationException("Missing client certificate")));
                        deviceClient = DeviceClient.Create(builder.HostName, this.edgeHostName, auth, this.deviceTransportSettings);
                    }

                    this.context.DeviceClientInstance = Option.Some(deviceClient);
                    Console.WriteLine("Leaf Device client created.");

                    var message = new Message(Encoding.ASCII.GetBytes($"Message from Leaf Device. Msg GUID: {this.context.MessageGuid}"));
                    Console.WriteLine($"Trying to send the message to '{this.edgeHostName}'");

                    while (!cts.IsCancellationRequested) // Retries are needed as the DeviceClient timeouts are not long enough
                    {
                        try
                        {
                            await deviceClient.SendEventAsync(message);
                            Console.WriteLine("Message Sent.");
                            await deviceClient.SetMethodHandlerAsync("DirectMethod", DirectMethod, null);
                            Console.WriteLine("Direct method callback is set.");
                            break;
                        }
                        catch (Exception e)
                        {
                            savedException = e;
                        }
                    }
                }
                catch (OperationCanceledException e)
                {
                    throw new InvalidOperationException("Failed to connect to edge and send data", savedException ?? e);
                }
                catch (Exception e)
                {
                    throw new InvalidOperationException("Failed to connect to edge and send data", e);
                }
            }
        }

        protected async Task GetOrCreateDeviceIdentityAsync()
        {
            var settings = new HttpTransportSettings();
            this.proxy.ForEach(p => settings.Proxy = p);
            IotHubConnectionStringBuilder builder = IotHubConnectionStringBuilder.Create(this.iothubConnectionString);
            RegistryManager rm = RegistryManager.CreateFromConnectionString(builder.ToString(), settings);

            Option<string> edgeScope = await this.edgeDeviceId
                .Map(id => GetScopeIfExitsAsync(rm, id))
                .GetOrElse(() => Task.FromResult<Option<string>>(Option.None<string>()));

            Device device = await rm.GetDeviceAsync(this.deviceId);
            if (device != null)
            {
                Console.WriteLine($"Device '{device.Id}' already registered on IoT hub '{builder.HostName}'");

                if (this.authType == AuthenticationType.SelfSigned)
                {
                    var thumbprints = this.thumbprints.Expect(() => new InvalidOperationException("Missing thumbprints list"));
                    if (!thumbprints.Contains(device.Authentication.X509Thumbprint.PrimaryThumbprint) ||
                        !thumbprints.Contains(device.Authentication.X509Thumbprint.SecondaryThumbprint))
                    {
                        // update the thumbprints before attempting to run any tests to ensure consistency
                        device.Authentication.X509Thumbprint = new X509Thumbprint { PrimaryThumbprint = thumbprints[0], SecondaryThumbprint = thumbprints[1] };
                    }
                }

                edgeScope.ForEach(s => device.Scope = s);
                await rm.UpdateDeviceAsync(device);

                this.context = new DeviceContext
                {
                    Device = device,
                    IotHubConnectionString = this.iothubConnectionString,
                    RegistryManager = rm,
                    RemoveDevice = false,
                    MessageGuid = Guid.NewGuid().ToString()
                };
            }
            else
            {
                await this.CreateDeviceIdentityAsync(rm, edgeScope);
            }
        }

        protected async Task VerifyDataOnIoTHubAsync()
        {
            var builder = new EventHubsConnectionStringBuilder(this.eventhubCompatibleEndpointWithEntityPath)
            {
                TransportType = this.eventHubClientTransportType
            };

            Console.WriteLine($"Receiving events from device '{this.context.Device.Id}' on Event Hub '{builder.EntityPath}'");

            EventHubClient eventHubClient =
                EventHubClient.CreateFromConnectionString(builder.ToString());

            this.proxy.ForEach(p => eventHubClient.WebProxy = p);

            PartitionReceiver eventHubReceiver = eventHubClient.CreateReceiver(
                "$Default",
                EventHubPartitionKeyResolver.ResolveToPartition(
                    this.context.Device.Id,
                    (await eventHubClient.GetRuntimeInformationAsync()).PartitionCount),
                EventPosition.FromEnqueuedTime(DateTime.Now.AddMinutes(-5)));

            var result = new TaskCompletionSource<bool>();
            using (var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3)))
            {
                using (cts.Token.Register(() => result.TrySetCanceled()))
                {
                    eventHubReceiver.SetReceiveHandler(
                        new PartitionReceiveHandler(
                            eventData =>
                            {
                                eventData.SystemProperties.TryGetValue("iothub-connection-device-id", out var devId);

                                if (devId != null && devId.ToString().Equals(this.context.Device.Id, StringComparison.Ordinal)
                                                  && Encoding.UTF8.GetString(eventData.Body).Contains(this.context.MessageGuid, StringComparison.Ordinal))
                                {
                                    result.TrySetResult(true);
                                    return true;
                                }

                                return false;
                            }));

                    await result.Task;
                }
            }

            await eventHubReceiver.CloseAsync();
            await eventHubClient.CloseAsync();
        }

        protected async Task VerifyDirectMethodAsync()
        {
            // User Service SDK to invoke Direct Method on the device.
            var settings = new ServiceClientTransportSettings();
            this.proxy.ForEach(p => settings.HttpProxy = p);
            ServiceClient serviceClient =
                ServiceClient.CreateFromConnectionString(this.context.IotHubConnectionString, this.serviceClientTransportType, settings);

            // Call a direct method
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(300)))
            {
                CloudToDeviceMethod cloudToDeviceMethod = new CloudToDeviceMethod("DirectMethod").SetPayloadJson("{\"TestKey\" : \"TestValue\"}");

                CloudToDeviceMethodResult result = await serviceClient.InvokeDeviceMethodAsync(
                    this.context.Device.Id,
                    cloudToDeviceMethod,
                    cts.Token);

                if (result.Status != 200)
                {
                    throw new Exception($"Could not invoke Direct Method on Device with result status {result.Status}.");
                }

                if (!result.GetPayloadAsJson().Equals("{\"TestKey\":\"TestValue\"}", StringComparison.Ordinal))
                {
                    throw new Exception($"Payload doesn't match with Sent Payload. Received payload: {result.GetPayloadAsJson()}. Expected: {{\"TestKey\":\"TestValue\"}}");
                }
            }
        }

        protected void KeepDeviceIdentity()
        {
            if (this.context != null)
            {
                this.context.RemoveDevice = false;
            }
        }

        protected Task MaybeDeleteDeviceIdentity()
        {
            if (this.context != null)
            {
                Device device = this.context.Device;
                bool remove = this.context.RemoveDevice;
                this.context.Device = null;

                if (remove)
                {
                    return this.context.RegistryManager.RemoveDeviceAsync(device);
                }
            }

            return Task.CompletedTask;
        }

        static void InstallTrustedCACerts(IEnumerable<X509Certificate2> trustedCertificates)
        {
            // Since Windows will pop up security warning when add certificate to current user store location;
            var name = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? StoreName.CertificateAuthority : StoreName.Root;
            var certsList = trustedCertificates.ToList();
            using (var store = new X509Store(name, StoreLocation.CurrentUser))
            {
                store.Open(OpenFlags.ReadWrite);
                foreach (var cert in certsList)
                {
                    store.Add(cert);
                }
            }
        }

        static Task<MethodResponse> DirectMethod(MethodRequest methodRequest, object userContext)
        {
            Console.WriteLine($"Leaf device received direct method call...Payload Received: {methodRequest.DataAsJson}");
            return Task.FromResult(new MethodResponse(methodRequest.Data, (int)HttpStatusCode.OK));
        }

        static async Task<Option<string>> GetScopeIfExitsAsync(RegistryManager rm, string deviceId)
        {
            Device edgeDevice = await rm.GetDeviceAsync(deviceId);
            if (edgeDevice == null)
            {
                return Option.None<string>();
            }

            Console.WriteLine($"Found Edge Device '{edgeDevice.Id}' registered in IoT hub with scope '{edgeDevice.Scope}'");
            return Option.Some(edgeDevice.Scope);
        }

        static (AuthenticationType,
            Option<X509Certificate2>,
            Option<IEnumerable<X509Certificate2>>,
            Option<List<string>>) ObtainAuthDetails(
                Option<DeviceCertificate> clientCertificatePaths,
                Option<IList<string>> thumbprintCertificatePaths) =>
            clientCertificatePaths.Map(
                clientCred =>
                {
                    (X509Certificate2 clientCert, IEnumerable<X509Certificate2> clientCertChain) =
                        CertificateHelper.GetServerCertificateAndChainFromFile(clientCred.CertificateFilePath, clientCred.PrivateKeyFilePath);
                    var authType = AuthenticationType.CertificateAuthority;
                    var thumbprintsOpt = thumbprintCertificatePaths.Map(
                        certificates =>
                        {
                            if (certificates.Count != 2)
                            {
                                throw new ArgumentException("Exactly two client thumbprint certificates expected");
                            }

                            if (string.IsNullOrWhiteSpace(certificates[0]) || !File.Exists(certificates[0]))
                            {
                                throw new ArgumentException($"'{certificates[0]}' is not a path to a thumbprint certificate file");
                            }

                            if (string.IsNullOrWhiteSpace(certificates[1]) || !File.Exists(certificates[1]))
                            {
                                throw new ArgumentException($"'{certificates[1]}' is not a path to a thumbprint certificate file");
                            }

                            authType = AuthenticationType.SelfSigned;
                            var rawCerts = new List<string>();
                            foreach (string dc in certificates)
                            {
                                string rawCert;
                                using (var sr = new StreamReader(dc))
                                {
                                    rawCert = sr.ReadToEnd();
                                }

                                rawCerts.Add(rawCert);
                            }

                            var certs = CertificateHelper.GetCertificatesFromPem(rawCerts);
                            var thumbprints = new List<string>();
                            foreach (var cert in certs)
                            {
                                thumbprints.Add(cert.Thumbprint.ToUpper(CultureInfo.InvariantCulture));
                            }

                            return thumbprints;
                        });

                    return (authType,
                        Option.Some(clientCert),
                        authType == AuthenticationType.CertificateAuthority ? Option.Some(clientCertChain) : Option.None<IEnumerable<X509Certificate2>>(),
                        thumbprintsOpt);
                }).GetOrElse(
                (AuthenticationType.Sas,
                    Option.None<X509Certificate2>(),
                    Option.None<IEnumerable<X509Certificate2>>(),
                    Option.None<List<string>>()));

        X509Certificate2 GetTrustedCertificate()
        {
            Console.WriteLine($"GetTrustedCertificate from: {this.trustedCACertificateFileName}");
            return new X509Certificate2(X509Certificate.CreateFromCertFile(this.trustedCACertificateFileName));
        }

        async Task CreateDeviceIdentityAsync(RegistryManager rm, Option<string> edgeDeviceScope)
        {
            var authMechanism = new AuthenticationMechanism { Type = this.authType };
            if (this.authType == AuthenticationType.SelfSigned)
            {
                authMechanism.X509Thumbprint = this.thumbprints.Map(
                    thList => { return new X509Thumbprint { PrimaryThumbprint = thList[0], SecondaryThumbprint = thList[1] }; }).GetOrElse(new X509Thumbprint());
            }

            var device = new Device(this.deviceId)
            {
                Authentication = authMechanism,
                Capabilities = new DeviceCapabilities { IotEdge = false },
            };
            edgeDeviceScope.ForEach(scope => device.Scope = scope);

            var builder = IotHubConnectionStringBuilder.Create(this.iothubConnectionString);
            Console.WriteLine($"Registering device '{device.Id}' on IoT hub '{builder.HostName}'");

            device = await rm.AddDeviceAsync(device);

            this.context = new DeviceContext
            {
                Device = device,
                DeviceClientInstance = Option.None<DeviceClient>(),
                IotHubConnectionString = this.iothubConnectionString,
                RegistryManager = rm,
                RemoveDevice = true,
                MessageGuid = Guid.NewGuid().ToString()
            };
        }
    }

    public class DeviceContext
    {
        public Device Device { get; set; }

        public Option<DeviceClient> DeviceClientInstance { get; set; }

        public string IotHubConnectionString { get; set; }

        public RegistryManager RegistryManager { get; set; }

        public bool RemoveDevice { get; set; }

        public string MessageGuid { get; set; } // used to identify exactly which message got sent.
    }
}
