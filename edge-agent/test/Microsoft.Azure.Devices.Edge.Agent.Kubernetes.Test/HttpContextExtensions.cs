// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Kubernetes.Test
{
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Http;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    public static class HttpContextExtensions
    {
        const string EndLine = "\r\n";

        public static async Task Write<T>(this HttpContext context, T item)
        {
            string line = JsonConvert.SerializeObject(item, new StringEnumConverter());
            await context.WriteStreamLine(line);
        }

        public static async Task WriteStreamLine(this HttpContext context, string line)
        {
            await context.Response.WriteAsync(line.Replace(EndLine, string.Empty));
            await context.Response.WriteAsync(EndLine);
            await context.Response.Body.FlushAsync();
        }
    }
}
