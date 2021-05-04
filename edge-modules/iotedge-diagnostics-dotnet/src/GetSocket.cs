// Copyright (c) Microsoft. All rights reserved.

namespace Diagnostics
{
    using System;
    using System.IO;
    using System.Net;
    using System.Net.Sockets;
    using System.Runtime.InteropServices;
    using System.Text;

    public class GetSocket
    {
        public static string GetSocketResponse(string server, string endpoint)
        {
            Uri uri = new Uri(server);
            string request = $"GET {endpoint} HTTP/1.1\r\nHost: {server}\r\nConnection: Close\r\n\r\n";
            byte[] bytesSent = Encoding.ASCII.GetBytes(request);
            byte[] bytesReceived = new byte[256];

            // Create a socket connection with the specified server and port.
            using (Socket socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.IP))
            {
                socket.ReceiveTimeout = 5000;
                socket.SendTimeout = 1000;

                socket.Connect(new UnixDomainSocketEndPoint(uri.LocalPath));

                // Send request to the server.
                socket.Send(bytesSent, bytesSent.Length, 0);
                int bytes = 0;
                string page = string.Empty;
                do
                {
                    bytes = socket.Receive(bytesReceived, bytesReceived.Length, 0);
                    page = page + Encoding.ASCII.GetString(bytesReceived, 0, bytes);
                }
                while (bytes > 0);

                return page;
            }
        }
    }
}
