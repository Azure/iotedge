using System.Diagnostics.Tracing;
using Azure.Core.Diagnostics;
using Azure.Identity;
using Microsoft.Azure.Devices;

using AzureEventSourceListener listener = new((args, message) =>
{
    if (args is { EventSource.Name: "Azure-Identity" })
    {
        Console.WriteLine(message);
    }
}, EventLevel.LogAlways);

var hostname = "EdgeConnectivityTestHub.azure-devices.net";
var settings = new HttpTransportSettings();
RegistryManager rm = RegistryManager.Create(hostname, new AzureCliCredential(), settings);

var deviceId = "ct1-Linux-amd64-connect-L0Wzdev6-Amqp-leaf";
var device = await rm.GetDeviceAsync(deviceId);
Console.WriteLine($"Device ID: {device.Id}, Status: {device.Status}, ETag: {device.ETag}");
