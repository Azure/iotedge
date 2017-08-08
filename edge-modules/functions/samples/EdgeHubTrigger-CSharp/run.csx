#r "Microsoft.WindowsAzure.Storage"
#r "System.Text.Encoding"
#r "Microsoft.Azure.Devices.Client"
#r "System.IO"

using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.Azure.Devices.Client;
using System.IO;

public static async Task Run(  
  Message messageReceived, 
  IAsyncCollector<Message> output, 
  ILogger log)
{
    byte[] byteArrayData = messageReceived.GetBytes();
    var txt = System.Text.Encoding.UTF8.GetString(byteArrayData);   
    log.LogInformation("EdgeHub trigger function processed a message: {txt}", txt);

    string dataBuffer = $"{{\"receivedData\":{txt},\"outputData\":Hello World!}}";
    Message eventMessage = new Message(System.Text.Encoding.UTF8.GetBytes(dataBuffer));
    await output.AddAsync(eventMessage);
}