// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Devices;

namespace common
{
    public class EdgeAgent : EdgeModule
    {
        public EdgeAgent() : base("edgeAgent") {}

        public async Task PingAsync(string hubConnectionString, string deviceId, CancellationToken token)
        {
            Exception savedException = null;

            try
            {
                var settings = new ServiceClientTransportSettings();
                ServiceClient client = ServiceClient.CreateFromConnectionString(
                    hubConnectionString,
                    TransportType.Amqp_WebSocket_Only,
                    settings);

                while (true)
                {
                    try
                    {
                        CloudToDeviceMethodResult result = await client.InvokeDeviceMethodAsync(
                            deviceId,
                            "$edgeAgent",
                            new CloudToDeviceMethod("ping"),
                            token);
                        if (result.Status == 200)
                        {
                            break;
                        }
                    }
                    catch (Exception e)
                    {
                        savedException = e;
                    }

                    await Task.Delay(TimeSpan.FromSeconds(5), token);
                }
            }
            catch (OperationCanceledException e)
            {
                string prefix = "Timed out while trying to ping module 'edgeAgent' from the cloud";
                throw new Exception(savedException == null
                    ? $"{prefix}: {e.Message}"
                    : $"{prefix}, exception from last attempt: {savedException.Message}");
            }
            catch (Exception e)
            {
                throw new Exception($"Failed to ping module 'edgeAgent' from the cloud: {e.Message}");
            }

            Console.WriteLine("Pinged module 'edgeAgent' from the cloud");
        }
    }
}