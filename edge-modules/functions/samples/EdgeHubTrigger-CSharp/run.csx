#r "Microsoft.WindowsAzure.Storage"
#r "System.Text.Encoding"
#r "Microsoft.Azure.Devices.Client"
#r "System.IO"
#r "Newtonsoft.Json"

using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.Azure.Devices.Client;
using System.IO;
using Newtonsoft.Json;

public static async Task Run(  
  Message messageReceived, 
  IAsyncCollector<Message> output, 
  ILogger log)
{
    const int DefaultTemperatureThreshold = 25;
    byte[] messageBytes = messageReceived.GetBytes();
    var messageString = System.Text.Encoding.UTF8.GetString(messageBytes);   
 
    // Get message body, containing the Temperature data         
    var messageBody = JsonConvert.DeserializeObject<MessageBody>(messageString); 
 
    if (messageBody != null && messageBody.Temperature > DefaultTemperatureThreshold)         
    {   
       var filteredMessage = new Message(messageBytes);             
       foreach (KeyValuePair<string, string> prop in messageReceived.Properties)             
       {                 
         filteredMessage.Properties.Add(prop.Key, prop.Value);             
       } 
       filteredMessage.Properties.Add("MessageType", "Alert"); 
       await output.AddAsync(filteredMessage);
    }
}
class MessageBody
{
  public int Temperature { get; set; }
}