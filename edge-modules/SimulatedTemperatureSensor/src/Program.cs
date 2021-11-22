// Copyright (c) Microsoft. All rights reserved.
namespace SimulatedTemperatureSensor
{
    using System;
    using System.IO;
    using System.Net;
    using System.Net.Security;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Transport.Mqtt;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Concurrency;
    using Microsoft.Azure.Devices.Edge.Util.TransientFaultHandling;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Configuration;
    using Newtonsoft.Json;
    using ExponentialBackoff = Microsoft.Azure.Devices.Edge.Util.TransientFaultHandling.ExponentialBackoff;
    using TransportType = Microsoft.Azure.Devices.Client.TransportType;

    class Program
    {
        public static int Main() => MainAsync().Result;

        static async Task<int> MainAsync()
        {
            Console.WriteLine("Basic module started.");
            ModuleClient moduleClient = await ModuleClient.CreateFromEnvironmentAsync();
            await moduleClient.OpenAsync();
            Console.WriteLine("Connected to EH.");

            for (int i = 0; i < 10000; i++)
            {
                await moduleClient.SendEventAsync(new Message(new byte[] { 1, 2, 3 }));

                Console.WriteLine("Sent message {0}", i);

                await Task.Delay(TimeSpan.FromSeconds(1));
            }

            return 0;
        }

        /*
        // static async Task<ModuleClient> CreateModuleClientAsync(
        //     TransportType transportType,
        //     ITransientErrorDetectionStrategy transientErrorDetectionStrategy = null,
        //     RetryStrategy retryStrategy = null)
        // {
        //     var retryPolicy = new RetryPolicy(transientErrorDetectionStrategy, retryStrategy);
        //     retryPolicy.Retrying += (_, args) => { Console.WriteLine($"[Error] Retry {args.CurrentRetryCount} times to create module client and failed with exception:{Environment.NewLine}{args.LastException}"); };

        //     ModuleClient client = await retryPolicy.ExecuteAsync(
        //         async () =>
        //         {
        //             ITransportSettings[] GetTransportSettings()
        //             {
        //                 RemoteCertificateValidationCallback certificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true;
        //                 switch (transportType)
        //                 {
        //                     case TransportType.Mqtt:
        //                     case TransportType.Mqtt_Tcp_Only:
        //                         return new ITransportSettings[] { new MqttTransportSettings(TransportType.Mqtt_Tcp_Only) };
        //                     case TransportType.Mqtt_WebSocket_Only:
        //                         return new ITransportSettings[] { new MqttTransportSettings(TransportType.Mqtt_WebSocket_Only) };
        //                     case TransportType.Amqp_WebSocket_Only:
        //                         return new ITransportSettings[] { new AmqpTransportSettings(TransportType.Amqp_WebSocket_Only) };
        //                     default:
        //                         Console.WriteLine("NEW CODE CHANGES -------------------");
        //                         var settings = new AmqpTransportSettings(TransportType.Amqp_Tcp_Only);
        //                         settings.RemoteCertificateValidationCallback = certificateValidationCallback;
        //                         return new ITransportSettings[]
        //                         {
        //                             settings
        //                         };
        //                 }
        //             }

        //             ITransportSettings[] settings = GetTransportSettings();
        //             Console.WriteLine($"[Information] [{DateTime.Now.ToLocalTime()}]: Trying to initialize module client using transport type [{transportType}].");

        //             ModuleClient moduleClient = ModuleClient.CreateFromConnectionString(Environment.GetEnvironmentVariable("auth"), settings);
        //             await moduleClient.OpenAsync();

        //             Console.WriteLine($"[Information] [{DateTime.Now.ToLocalTime()}]: Successfully initialized module client of transport type [{transportType}].");
        //             return moduleClient;
        //         });

        //     return client;
        // }
        */
    }
}
