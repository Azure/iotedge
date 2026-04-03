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
    using Microsoft.Azure.Devices.Edge.Test.Common;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.EventHubs;
    using EventHubClientTransportType = Microsoft.Azure.EventHubs.TransportType;

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
        readonly EventHubClientTransportType eventHubClientTransportType;
        readonly IotHubClientTransportSettings deviceTransportSettings;
        readonly ClientAuthenticationType authType = ClientAuthenticationType.None;
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
            if (!string.IsNullOrWhiteSpace(edgeDeviceId))
            {
                this.edgeDeviceId = Option.Some(edgeDeviceId);
            }

            (this.authType,
                this.clientCertificate,
                this.clientCertificateChain,
                this.thumbprints) = ObtainAuthDetails(clientCertificatePaths, thumbprintCertificatePaths);

            if (protocol == DeviceProtocol.AmqpWS || protocol == DeviceProtocol.MqttWs)
            {
                this.eventHubClientTransportType = EventHubClientTransportType.AmqpWebSockets;
                if (protocol == DeviceProtocol.MqttWs)
                {
                    this.deviceTransportSettings = new IotHubClientMqttSettings(IotHubClientTransportProtocol.WebSocket);
                }
                else
                {
                    this.deviceTransportSettings = new IotHubClientAmqpSettings(IotHubClientTransportProtocol.WebSocket);
                }
            }
            else
            {
                this.eventHubClientTransportType = this.proxy.HasValue ? EventHubClientTransportType.AmqpWebSockets : EventHubClientTransportType.Amqp;
                if (protocol == DeviceProtocol.Mqtt)
                {
                    this.deviceTransportSettings = new IotHubClientMqttSettings(IotHubClientTransportProtocol.Tcp);
                }
                else
                {
                    this.deviceTransportSettings = new IotHubClientAmqpSettings(IotHubClientTransportProtocol.Tcp);
                }
            }

            Console.WriteLine(
                $"Leaf Device Client: \n"
                + $"\t[authType={this.authType}] \n"
                + $"\t[clientCertificate subject name={this.clientCertificate.Match(c => c.SubjectName.ToString(), () => string.Empty)}] \n"
                + $"\t[clientCertificateChain count={this.clientCertificateChain.Match(c => c.Count(), () => 0)}] \n"
                + $"\t[event hub client transport type={this.eventHubClientTransportType}]\n"
                + $"\t[device transport type={this.deviceTransportSettings.GetType().Name}]");
        }

        protected Task InitializeTrustedCertsAsync()
        {
            if (!string.IsNullOrEmpty(this.trustedCACertificateFileName))
            {
                // Windows will pop up security warning when add certificate to current user store location, so the tests won't run automatically;
                // Therefore we will use CustomCertificateValidator instead.
                // Since Microsoft.Azure.Devices.Client v1.23.0 release, the only e2e test that fails on Windows if the
                // CustomCertificateValidator workaround is removed is Quickstart Certs test
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
                    var csProperties = ParseConnectionString(this.iothubConnectionString);
                    IotHubDeviceClient deviceClient;
                    var options = new IotHubClientOptions(this.deviceTransportSettings);
                    if (this.authType == ClientAuthenticationType.Sas)
                    {
                        string leafDeviceConnectionString = $"HostName={csProperties["HostName"]};DeviceId={this.deviceId};SharedAccessKey={this.context.Device.Authentication.SymmetricKey.PrimaryKey};GatewayHostName={this.edgeHostName}";
                        deviceClient = new IotHubDeviceClient(leafDeviceConnectionString, options);
                    }
                    else
                    {
                        var auth = new ClientAuthenticationWithX509Certificate(this.clientCertificate.Expect(() => new InvalidOperationException("Missing client certificate")), this.deviceId);
                        options.GatewayHostName = this.edgeHostName;
                        deviceClient = new IotHubDeviceClient(csProperties["HostName"], auth, options);
                    }

                    this.context.DeviceClientInstance = Option.Some(deviceClient);
                    Console.WriteLine("Leaf Device client created.");

                    var message = new TelemetryMessage(Encoding.ASCII.GetBytes($"Message from Leaf Device. Msg GUID: {this.context.MessageGuid}"));
                    Console.WriteLine($"Trying to send the message to '{this.edgeHostName}'");

                    while (!cts.IsCancellationRequested) // Retries are needed as the DeviceClient timeouts are not long enough
                    {
                        try
                        {
                            await deviceClient.SendTelemetryAsync(message);
                            if (string.IsNullOrWhiteSpace(this.context.Device.Scope))
                            {
                                throw new InvalidOperationException("Expected to throw exception");
                            }

                            Console.WriteLine("Message Sent.");
                            await deviceClient.SetDirectMethodCallbackAsync(DirectMethodHandler);
                            Console.WriteLine("Direct method callback is set.");
                            break;
                        }
                        catch (InvalidOperationException) when (string.IsNullOrWhiteSpace(this.context.Device.Scope))
                        {
                            Console.WriteLine("Expected exception was not thrown");
                            throw;
                        }
                        catch (UnauthorizedAccessException ex) when (!string.IsNullOrWhiteSpace(this.context.Device.Scope))
                        {
                            Console.WriteLine("Expected exception {0}", ex);
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
            var csProperties = ParseConnectionString(this.iothubConnectionString);
            IotHubServiceClient serviceClient = new IotHubServiceClient(this.iothubConnectionString);

            Option<string> edgeScope = await this.edgeDeviceId
                .Map(id => GetScopeIfExitsAsync(serviceClient, id))
                .GetOrElse(() => Task.FromResult<Option<string>>(Option.None<string>()));

            Device device = await serviceClient.Devices.GetAsync(this.deviceId);
            if (device != null)
            {
                Console.WriteLine($"Device '{device.Id}' already registered on IoT hub '{csProperties["HostName"]}'");

                if (this.authType == ClientAuthenticationType.SelfSigned)
                {
                    var thumbprintsList = this.thumbprints.Expect(() => new InvalidOperationException("Missing thumbprints list"));
                    if (!thumbprintsList.Contains(device.Authentication.X509Thumbprint.PrimaryThumbprint) ||
                        !thumbprintsList.Contains(device.Authentication.X509Thumbprint.SecondaryThumbprint))
                    {
                        // update the thumbprints before attempting to run any tests to ensure consistency
                        device.Authentication.X509Thumbprint = new X509Thumbprint { PrimaryThumbprint = thumbprintsList[0], SecondaryThumbprint = thumbprintsList[1] };
                    }
                }

                edgeScope.ForEach(s => device.Scope = s);
                await serviceClient.Devices.SetAsync(device, true);

                this.context = new DeviceContext
                {
                    Device = device,
                    IotHubConnectionString = this.iothubConnectionString,
                    ServiceClient = serviceClient,
                    RemoveDevice = false,
                    MessageGuid = Guid.NewGuid().ToString()
                };
            }
            else
            {
                await this.CreateDeviceIdentityAsync(serviceClient, edgeScope);
            }
        }

        protected async Task VerifyDataOnIoTHubAsync()
        {
            // Leaf device without parent not expected to send messages
            if (!this.edgeDeviceId.HasValue)
                return;

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
            // Leaf device without parent not expected to succed dm
            if (!this.edgeDeviceId.HasValue)
                return;

            // Use Service SDK to invoke Direct Method on the device.
            IotHubServiceClient serviceClient = new IotHubServiceClient(this.context.IotHubConnectionString);

            // Call a direct method
            TimeSpan testDuration = TimeSpan.FromSeconds(300);
            DateTime endTime = DateTime.UtcNow + testDuration;

            DirectMethodServiceRequest directMethodRequest = new DirectMethodServiceRequest("DirectMethod");
            directMethodRequest.SetPayloadJson("{\"TestKey\" : \"TestValue\"}");

            DirectMethodClientResponse result = null;
            // To reduce log size and make troubleshooting easier, log last exception only.
            Exception lastException = null;
            bool isRetrying = true;

            Console.WriteLine("Starting Direct method test.");
            while (isRetrying && DateTime.UtcNow <= endTime)
            {
                try
                {
                    using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                    {
                        result = await serviceClient.DirectMethods.InvokeAsync(
                            this.context.Device.Id,
                            directMethodRequest,
                            cts.Token);

                        if (result?.Status == 200)
                        {
                            isRetrying = false;
                        }

                        // Don't retry too fast
                        await Task.Delay(1000, cts.Token);
                    }
                }
                catch (OperationCanceledException ex)
                {
                    if (lastException == null)
                    {
                        lastException = ex;
                    }
                }
                catch (Exception ex)
                {
                    lastException = ex;
                }
            }

            if (result?.Status != 200)
            {
                if (lastException != null)
                {
                    Console.WriteLine($"Failed to send direct method from device '{this.context.Device.Id}' with payload '{directMethodRequest}: {lastException}'");
                }

                throw new Exception($"Could not invoke Direct Method on Device with result status {result?.Status}.");
            }

            if (!string.Equals(result.JsonPayload.GetRawText(), "{\"TestKey\":\"TestValue\"}", StringComparison.Ordinal))
            {
                throw new Exception($"Payload doesn't match with Sent Payload. Received payload: {result.JsonPayload}. Expected: {{\"TestKey\":\"TestValue\"}}");
            }

            Console.WriteLine("Direct method test passed.");
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
                    return this.context.ServiceClient.Devices.DeleteAsync(device.Id);
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

        static Task<DirectMethodResponse> DirectMethodHandler(DirectMethodRequest methodRequest)
        {
            Console.WriteLine($"Leaf device received direct method call...Payload Received: {System.Text.Encoding.UTF8.GetString(methodRequest.Payload)}");
            var response = new DirectMethodResponse((int)HttpStatusCode.OK);
            response.SetPayloadJson(System.Text.Encoding.UTF8.GetString(methodRequest.Payload));
            return Task.FromResult(response);
        }

        static async Task<Option<string>> GetScopeIfExitsAsync(IotHubServiceClient serviceClient, string deviceId)
        {
            Device edgeDevice = await serviceClient.Devices.GetAsync(deviceId);
            if (edgeDevice == null)
            {
                return Option.None<string>();
            }

            Console.WriteLine($"Found Edge Device '{edgeDevice.Id}' registered in IoT hub with scope '{edgeDevice.Scope}'");
            return Option.Some(edgeDevice.Scope);
        }

        static (ClientAuthenticationType,
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
                    var authType = ClientAuthenticationType.CertificateAuthority;
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

                            authType = ClientAuthenticationType.SelfSigned;
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
                            var thumbprintList = new List<string>();
                            foreach (var cert in certs)
                            {
                                thumbprintList.Add(cert.Thumbprint.ToUpper(CultureInfo.InvariantCulture));
                            }

                            return thumbprintList;
                        });

                    return (authType,
                        Option.Some(clientCert),
                        authType == ClientAuthenticationType.CertificateAuthority ? Option.Some(clientCertChain) : Option.None<IEnumerable<X509Certificate2>>(),
                        thumbprintsOpt);
                }).GetOrElse(
                (ClientAuthenticationType.Sas,
                    Option.None<X509Certificate2>(),
                    Option.None<IEnumerable<X509Certificate2>>(),
                    Option.None<List<string>>()));

        X509Certificate2 GetTrustedCertificate()
        {
            Console.WriteLine($"GetTrustedCertificate from: {this.trustedCACertificateFileName}");
            return X509CertificateLoader.LoadCertificateFromFile(this.trustedCACertificateFileName);
        }

        async Task CreateDeviceIdentityAsync(IotHubServiceClient serviceClient, Option<string> edgeDeviceScope)
        {
            var authMechanism = new AuthenticationMechanism { Type = this.authType };
            if (this.authType == ClientAuthenticationType.SelfSigned)
            {
                authMechanism.X509Thumbprint = this.thumbprints.Map(
                    thList => { return new X509Thumbprint { PrimaryThumbprint = thList[0], SecondaryThumbprint = thList[1] }; }).GetOrElse(new X509Thumbprint());
            }

            var device = new Device(this.deviceId)
            {
                Authentication = authMechanism,
                Capabilities = new ClientCapabilities { IsIotEdge = false },
            };
            edgeDeviceScope.ForEach(scope => device.Scope = scope);

            var csProperties = ParseConnectionString(this.iothubConnectionString);
            Console.WriteLine($"Registering device '{device.Id}' on IoT hub '{csProperties["HostName"]}'");

            device = await serviceClient.Devices.CreateAsync(device);

            this.context = new DeviceContext
            {
                Device = device,
                DeviceClientInstance = Option.None<IotHubDeviceClient>(),
                IotHubConnectionString = this.iothubConnectionString,
                ServiceClient = serviceClient,
                RemoveDevice = true,
                MessageGuid = Guid.NewGuid().ToString()
            };
        }

        static Dictionary<string, string> ParseConnectionString(string connectionString) =>
            connectionString.Split(';')
                .Select(part => part.Split(new[] { '=' }, 2))
                .Where(parts => parts.Length == 2)
                .ToDictionary(parts => parts[0].Trim(), parts => parts[1].Trim());
    }

    public class DeviceContext
    {
        public Device Device { get; set; }

        public Option<IotHubDeviceClient> DeviceClientInstance { get; set; }

        public string IotHubConnectionString { get; set; }

        public IotHubServiceClient ServiceClient { get; set; }

        public bool RemoveDevice { get; set; }

        public string MessageGuid { get; set; } // used to identify exactly which message got sent.
    }
}
