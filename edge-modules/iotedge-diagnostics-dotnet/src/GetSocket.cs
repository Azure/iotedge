// Copyright (c) Microsoft. All rights reserved.

namespace Diagnostics
{
    using System;
    using System.IO;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;

    public class GetSocket
    {
        public static string GetSocketResponse(string server, string endpoint)
        {
            string request = $"GET {endpoint} HTTP/1.1\r\nHost: {server}\r\nConnection: Close\r\n\r\n";
            byte[] bytesSent = Encoding.ASCII.GetBytes(request);
            byte[] bytesReceived = new byte[256];
            server = server.Replace("unix://", string.Empty);
            string page = string.Empty;

            // Create a socket connection with the specified server and port.
            using (Socket socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.IP))
            {
                try
                {
                    socket.ReceiveTimeout = 5000;
                    socket.SendTimeout = 1000;

                    socket.Connect(new UnixDomainSocketEndPoint(server));

                    // Send request to the server.
                    socket.Send(bytesSent, bytesSent.Length, 0);
                    int bytes = 0;

                    do
                    {
                        bytes = socket.Receive(bytesReceived, bytesReceived.Length, 0);
                        page = page + Encoding.ASCII.GetString(bytesReceived, 0, bytes);
                    }
                    while (bytes > 0);
                }
                catch (SocketException e)
                {
                    Console.Error.WriteLine(string.Format("SocketError - SocketErrorCode ({0}) : {1}", e.SocketErrorCode, e.Message));
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine(e.Message);
                }

                return page;
            }
        }
    }
}