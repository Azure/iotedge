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

    if (messageBody != null && messageBody.Machine.Temperature > DefaultTemperatureThreshold)         
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

/// <summary>
///Body:
///{
///  “machine”:{
///    “temperature”:,
///    “pressure”:
///  },
///  “ambient”:{
///    “temperature”: , 
///    “humidity”:
///  }
///  “timeCreated”:”UTC iso format”
///}
///Units and types:
///Temperature: double, C
///Humidity: int, %
///Pressure: double, psi
/// </summary>
public class MessageBody
{
    public Machine Machine { get; set; }

    public Ambient Ambient { get; set; }

    public DateTime TimeCreated { get; set; }
}

public class Machine
{
    public double Temperature { get; set; }

    public double Pressure { get; set; }
}

public class Ambient
{
    public double Temperature { get; set; }

    public int Humidity { get; set; }
}