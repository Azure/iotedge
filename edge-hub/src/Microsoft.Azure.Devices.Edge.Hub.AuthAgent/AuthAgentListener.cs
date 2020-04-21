// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.AuthAgent
{
    using System;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Util;

    using Microsoft.Extensions.Logging;
    
    public class AuthAgentListener : IProtocolHead
    {
        private IAuthenticator authenticator;
        private HttpListener listener;

        private Task listenerTask;
        private bool exitListenerLoop;

        // FIXME: should come from config
        private static readonly string listeningAddress = "http://localhost:7120/authenticate/";
        private const int maxInBufferSize = 32 * 1024;

        public AuthAgentListener(IAuthenticator authenticator)
        {
            this.authenticator = authenticator;
        }

        public string Name => "AUTH";

        public Task StartAsync()
        {
            Events.Starting();
            this.Start();
            Events.Started();

            return Task.CompletedTask;
        }

        public async Task CloseAsync(CancellationToken token)
        {
            Events.Closing();

            var currentListener = Interlocked.Exchange(ref this.listener, null);
            if (currentListener == null)
            {
                Events.ClosedWhenNotRunning();
                return;
            }

            // this disposes the instance making the listener loop exit
            Volatile.Write(ref exitListenerLoop, true);
            currentListener.Close();

            await this.listenerTask;
            this.listenerTask = null;

            Events.Closed();
        }

        public void Dispose() => this.CloseAsync(CancellationToken.None).Wait();

        private void Start()
        {
            var newListener = new HttpListener();

            if (Interlocked.CompareExchange(ref this.listener, newListener, null) != null)
            {
                newListener.Close();
                Events.StartedWhenAlreadyRunning();
                throw new InvalidOperationException("Cannot start AuthAgent twice");
            }

            Volatile.Write(ref this.exitListenerLoop, false);

            this.listener.Prefixes.Add(listeningAddress);
            this.listener.Start();

            this.listenerTask = ListenerLoop();
            this.listenerTask.ContinueWith(_ => Restart());
        }

        private void Restart()
        {
            // no need for restart, stopped on purpose
            if (Volatile.Read(ref exitListenerLoop))
            {
                return;
            }

            var prevListener = Interlocked.Exchange(ref this.listener, null);
            prevListener?.Close();

            Events.Restarting();
            Start();
        }

        private async Task ListenerLoop()
        {
            while (true)
            {
                try
                {
                    var context = await this.listener.GetContextAsync();
                    var request = context.Request;

                    var requestString = await ReadRequestBodyAsString(request);

                    ////////////////////////////////////////////////////////
                    /// SENDING BACK SOMETHING - WILL BE REWRITTEN FROM HERE:

                    var response = context.Response;
                    string responseString = @"{""result"":""OK"", ""identity"":""device_1/module_2""}";
                    byte[] buffer = Encoding.UTF8.GetBytes(responseString);

                    response.ContentType = "application/json; charset=utf-8";
                    response.ContentEncoding = Encoding.UTF8;
                    
                    //response.ContentLength64 = buffer.Length;
                    System.IO.Stream output = response.OutputStream;
                    output.Write(buffer, 0, buffer.Length);
                    output.Close();
                }
                catch (ObjectDisposedException e) when (e.ObjectName == typeof(HttpListener).FullName)
                {
                    Events.ShutdownListenerLoop();
                    break;
                }
                catch (Exception e)
                {
                    Events.ErrorExitListenerLoop(e);
                    break;
                }
            }
        }

        private async Task<Option<string>> ReadRequestBodyAsString(HttpListenerRequest request)
        {
            var requestBodyBuffer = GetRequestBodyBuffer(request);

            var result = default(string);
            var bytesRead = default(int);

            using (var inputStream = request.InputStream)
            {
                try
                {
                    bytesRead = await request.InputStream.ReadAsync(requestBodyBuffer, 0, requestBodyBuffer.Length);
                }
                catch (Exception e)
                {
                    Events.ErrorReadingStream(e);
                    return Option.None<String>();
                }

                // read as many bytes as the buffer capacity. Remember that we allocated +1 byte,
                // so we don't expect that the buffer will be full. If it is, the Content-Length was
                // wrong, or the request is bigger than the maximum allowed.
                if (bytesRead == requestBodyBuffer.Length)
                {
                    Events.InvalidRequest();
                    return Option.None<String>();
                }

                try
                {
                    result = Encoding.UTF8.GetString(requestBodyBuffer, 0, bytesRead);
                }
                catch (Exception e)
                {
                    Events.ErrorDecodingStream(e);
                    return Option.None<String>();
                }
            }

            return Option.Some<String>(result);
        }

        private byte[] GetRequestBodyBuffer(HttpListenerRequest request)
        {
            // the request may contain the content-length in the header - or not
            var requestSize = request.ContentLength64 >= 0 ? request.ContentLength64 : maxInBufferSize;

            // if the client sends too big number in the header - don't believe it
            // also, overallocate the buffer by one, so at the end we should read less bytes than requested
            requestSize = Math.Min(maxInBufferSize, requestSize + 1);
            return new byte[requestSize];
        }

        static class Events
        {
            const int IdStart = AuthAgentEventIds.AuthAgentListener;
            static readonly ILogger Log = Logger.Factory.CreateLogger<AuthAgentListener>();

            enum EventIds
            {
                Starting = IdStart,
                Restarting,
                Started,
                Closing,
                Closed,
                ClosedWhenNotRunning,
                StartedWhenAlreadyRunning,
                ShutdownListenerLoop,
                ErrorExitListenerLoop,
                InvalidRequest,
                ErrorReadingStream,
                ErrorDecodingStream
            }

            public static void Starting()
            {
                Log.LogInformation((int)EventIds.Starting, "Starting AUTH head");
            }

            public static void Restarting()
            {
                Log.LogInformation((int)EventIds.Restarting, "Restarting AUTH head after error");
            }

            public static void Started()
            {
                Log.LogInformation((int)EventIds.Started, "Started AUTH head");
            }

            public static void Closing()
            {
                Log.LogInformation((int)EventIds.Closing, "Closing AUTH head");
            }

            public static void Closed()
            {
                Log.LogInformation((int)EventIds.Closed, "Closed AUTH head");
            }

            public static void ClosedWhenNotRunning()
            {
                Log.LogInformation((int)EventIds.ClosedWhenNotRunning, "Closed AUTH head when it was not running");
            }

            public static void StartedWhenAlreadyRunning()
            {
                Log.LogWarning((int)EventIds.StartedWhenAlreadyRunning, "Started AUTH head when it was already running");
            }

            public static void ShutdownListenerLoop()
            {
                Log.LogInformation((int)EventIds.ShutdownListenerLoop, "Exiting AUTH listener loop by closing protocol head");
            }

            public static void ErrorExitListenerLoop(Exception e)
            {
                Log.LogWarning((int)EventIds.ErrorExitListenerLoop, e, "Exiting AUTH listener loop by error");
            }

            public static void InvalidRequest()
            {
                Log.LogWarning((int)EventIds.InvalidRequest, "Invalid request - ignoring");
            }

            public static void ErrorReadingStream(Exception e)
            {
                Log.LogWarning((int)EventIds.ErrorReadingStream, e, "Error reading AUTH request");
            }

            public static void ErrorDecodingStream(Exception e)
            {
                Log.LogWarning((int)EventIds.ErrorDecodingStream, e, "Error decoding AUTH request, UTF8 encoding expected");
            }            
        }
    }
}
