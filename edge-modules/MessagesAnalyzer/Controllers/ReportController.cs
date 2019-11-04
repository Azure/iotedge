// Copyright (c) Microsoft. All rights reserved.
namespace MessagesAnalyzer.Controllers
{
    using System.Text;
    using System.Web;
    using System.Runtime.Serialization.Formatters.Binary;
    using System.IO;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.Devices.Edge.Util.AzureLogAnalytics;

    [Route("api/[controller]")]
    [ApiController]
    public class ReportController : Controller
    {
        // Modified from https://stackoverflow.com/questions/33022660/how-to-convert-byte-array-to-any-type
        byte[] TypeToBytes<T>(T obj)
        {
            if(obj == null)
                return null;
            BinaryFormatter bf = new BinaryFormatter();
            using(MemoryStream ms = new MemoryStream())
            {
                bf.Serialize(ms, obj);
                return ms.ToArray();
            }
        }

        T BytesToType<T>(byte[] data)
        {
            if(data == null)
                return default(T);
            BinaryFormatter bf = new BinaryFormatter();
            using(MemoryStream ms = new MemoryStream(data))
            {
                object obj = bf.Deserialize(ms);
                return (T)obj;
            }
        }

        // GET api/report
        [HttpGet]
        public ActionResult<string> Get()
        {
            string resultJson = Reporter.GetReceivedMessagesReport(Settings.Current.ToleranceInMilliseconds).ToString();

            if (Settings.Current.LogAnalyticEnabled)
            {
                // A controller in ASP.net is created everytime there is a request.
                // This means the value attribute does not persist between requests.
                // Here, we are using HttpContext session to store value for inter-request use.

                AzureLogAnalytics logAnalytics = null;
                const string logAnalyticsVarKey = "logAnalytics";
                byte[] logAnalyticsBytes;
                if(HttpContext.Session.TryGetValue(logAnalyticsVarKey, out logAnalyticsBytes))
                {
                    logAnalytics = BytesToType<AzureLogAnalytics>(logAnalyticsBytes);
                }
                else
                {
                    logAnalytics = new AzureLogAnalytics(
                        Settings.Current.LogAnalyticWorkspaceId,
                        Settings.Current.LogAnalyticSharedKey,
                        Settings.Current.LogAnalyticLogType);
                    HttpContext.Session.Set(logAnalyticsVarKey, TypeToBytes<AzureLogAnalytics>(logAnalytics));
                }

                // Upload the data to Log Analytics
                logAnalytics.Post(Encoding.UTF8.GetBytes(resultJson));
            }

            return resultJson;
        }
    }
}
