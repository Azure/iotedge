// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Amqp
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Amqp;
    using Microsoft.Azure.Amqp.Framing;
    using Microsoft.Azure.Devices.Common;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Concurrency;
    using Microsoft.Extensions.Logging;
    using CoreConstants = Microsoft.Azure.Devices.Edge.Hub.Core.Constants;

    /// <summary>
    /// This class is used to get tokens from the Client on the CBS link. It generates
    /// an identity from the received token and authenticates it.
    /// </summary>
    class CbsNode : ICbsNode, IAmqpAuthenticator
    {
        static readonly List<UriPathTemplate> ResourceTemplates = new List<UriPathTemplate>
        {
            Templates.DeviceResourceTemplate,
            Templates.ModuleResourceTemplate
        };

        readonly object sendingLinkSyncLock = new object();
        readonly object receivingLinkSyncLock = new object();
        readonly AsyncLock identitySyncLock = new AsyncLock();
        readonly IClientCredentialsFactory clientCredentialsFactory;
        readonly IAuthenticator authenticator;
        readonly string iotHubHostName;
        readonly ICredentialsCache credentialsCache;
        readonly ConcurrentDictionary<string, ClientToken> clientTokens = new ConcurrentDictionary<string, ClientToken>();
        readonly ConcurrentDictionary<string, byte> authenticatedClients = new ConcurrentDictionary<string, byte>();
        bool disposed;

        ISendingAmqpLink sendingLink;
        IReceivingAmqpLink receivingLink;
        int deliveryCount;

        public CbsNode(IClientCredentialsFactory identityFactory, string iotHubHostName, IAuthenticator authenticator, ICredentialsCache credentialsCache)
        {
            this.clientCredentialsFactory = identityFactory;
            this.iotHubHostName = iotHubHostName;
            this.authenticator = authenticator;
            this.credentialsCache = credentialsCache;
        }

        public void RegisterLink(IAmqpLink link)
        {
            if (link.IsReceiver)
            {
                lock (this.receivingLinkSyncLock)
                {
                    this.receivingLink = (EdgeReceivingAmqpLink)link;
                    this.receivingLink.RegisterMessageListener(this.OnMessageReceived);
                }
            }
            else
            {
                lock (this.sendingLinkSyncLock)
                {
                    this.sendingLink = (EdgeSendingAmqpLink)link;
                }
            }

            Events.LinkRegistered(link);
        }

        public async Task<bool> AuthenticateAsync(string id, Option<string> modelId, Option<string> authChain)
        {
            try
            {
                // See if we received direct credentials for the target identity
                bool hasToken = this.clientTokens.TryGetValue(id, out ClientToken clientToken);

                // Otherwise, this could be a child Edge acting OnBehalfOf of the target,
                // in which case we would have the actor's credentials instead
                if (!hasToken)
                {
                    Option<string> actorDeviceId = AuthChainHelpers.GetActorDeviceId(authChain);
                    hasToken = actorDeviceId.Map(actor => this.clientTokens.TryGetValue($"{actor}/{CoreConstants.EdgeHubModuleId}", out clientToken)).GetOrElse(false);
                }

                if (hasToken)
                {
                    // Lock the auth-state check and the auth call to avoid
                    // redundant calls into the authenticator
                    using (await this.identitySyncLock.LockAsync())
                    {
                        if (this.authenticatedClients.ContainsKey(id))
                        {
                            // We've previously authenticated this client already
                            return true;
                        }
                        else
                        {
                            IClientCredentials clientCredentials = this.clientCredentialsFactory.GetWithSasToken(
                                clientToken.DeviceId,
                                clientToken.ModuleId.OrDefault(),
                                string.Empty,
                                clientToken.Token,
                                true,
                                modelId,
                                authChain);

                            if (await this.authenticator.AuthenticateAsync(clientCredentials))
                            {
                                // Authentication success, add an entry for the authenticated
                                // client identity, the value in the dictionary doesn't matter
                                // as we're effectively using it as a thread-safe HashSet
                                this.authenticatedClients[id] = default(byte);

                                // No need to add the new credentials to the cache, as the
                                // authenticator already implicitly handles it
                                return true;
                            }
                        }
                    }
                }

                return false;
            }
            catch (Exception e)
            {
                Events.ErrorAuthenticatingIdentity(id, e);
                return false;
            }
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposed)
            {
                this.disposed = true;
            }
        }

        internal static (string token, string audience) ValidateAndParseMessage(string iotHubHostName, AmqpMessage message)
        {
            string type = message.ApplicationProperties.Map[CbsConstants.PutToken.Type] as string;
            if (!CbsConstants.SupportedTokenTypes.Any(t => string.Equals(type, t, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException("Cbs message missing Type property");
            }

            if (string.IsNullOrEmpty(message.ApplicationProperties.Map[CbsConstants.PutToken.Audience] as string))
            {
                throw new InvalidOperationException("Cbs message missing audience property");
            }

            if (!(message.ApplicationProperties.Map[CbsConstants.Operation] is string operation)
                || !operation.Equals(CbsConstants.PutToken.OperationValue, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Cbs message missing operation value {CbsConstants.PutToken.OperationValue}");
            }

            string token = message.ValueBody.Value as string;
            if (string.IsNullOrEmpty(token))
            {
                throw new InvalidOperationException("Cbs message does not contain a valid token");
            }

            try
            {
                SharedAccessSignature sharedAccessSignature = SharedAccessSignature.Parse(iotHubHostName, token);
                return (token, sharedAccessSignature.Audience);
            }
            catch (Exception e)
            {
                throw new InvalidOperationException("Cbs message does not contain a valid token", e);
            }
        }

        internal static (string deviceId, string moduleId) ParseIds(string audience)
        {
            string decodedAudience = WebUtility.UrlDecode(audience);
            string audienceUri = decodedAudience.StartsWith("amqps://", StringComparison.CurrentCultureIgnoreCase) ? decodedAudience : "amqps://" + decodedAudience;

            foreach (UriPathTemplate template in ResourceTemplates)
            {
                (bool success, IList<KeyValuePair<string, string>> boundVariables) = template.Match(new Uri(audienceUri));
                if (success)
                {
                    IDictionary<string, string> boundVariablesDictionary = boundVariables.ToDictionary();
                    string deviceId = boundVariablesDictionary[Templates.DeviceIdTemplateParameterName];
                    string moduleId = boundVariablesDictionary.ContainsKey(Templates.ModuleIdTemplateParameterName)
                        ? boundVariablesDictionary[Templates.ModuleIdTemplateParameterName]
                        : null;
                    return (deviceId, moduleId);
                }
            }

            throw new InvalidOperationException($"Matching template not found for audience {audienceUri}");
        }

        internal async Task<(AmqpResponseStatusCode, string)> UpdateCbsToken(AmqpMessage message)
        {
            ClientToken clientToken;

            try
            {
                clientToken = this.GetClientToken(message);
            }
            catch (Exception e) when (!ExceptionEx.IsFatal(e))
            {
                Events.ErrorGettingIdentity(e);
                return (AmqpResponseStatusCode.BadRequest, e.Message);
            }

            bool isNewClient = !this.clientTokens.ContainsKey(clientToken.Id);

            // Insert/update the cached tokens with the new value
            this.clientTokens[clientToken.Id] = clientToken;

            if (this.authenticatedClients.ContainsKey(clientToken.Id))
            {
                IClientCredentials clientCredentials = this.clientCredentialsFactory.GetWithSasToken(
                    clientToken.DeviceId,
                    clientToken.ModuleId.OrDefault(),
                    string.Empty,
                    clientToken.Token,
                    true,
                    Option.None<string>(),
                    Option.None<string>());

                await this.credentialsCache.Add(clientCredentials);
                Events.CbsTokenUpdated(clientToken.Id);
            }
            else if (!isNewClient)
            {
                Events.CbsTokenNotUpdated(clientToken.Id);
            }

            return (AmqpResponseStatusCode.OK, AmqpResponseStatusCode.OK.ToString());
        }

        internal ClientToken GetClientToken(AmqpMessage message)
        {
            (string token, string audience) = ValidateAndParseMessage(this.iotHubHostName, message);
            (string deviceId, string moduleId) = ParseIds(audience);
            return new ClientToken(deviceId, Option.Maybe(moduleId), token);
        }

        internal ArraySegment<byte> GetDeliveryTag()
        {
            int deliveryId = Interlocked.Increment(ref this.deliveryCount);
            return new ArraySegment<byte>(BitConverter.GetBytes(deliveryId));
        }

        static AmqpMessage GetAmqpResponse(AmqpMessage requestMessage, AmqpResponseStatusCode statusCode, string statusDescription)
        {
            AmqpMessage response = AmqpMessage.Create();
            response.Properties.CorrelationId = requestMessage.Properties.MessageId;
            response.ApplicationProperties = new ApplicationProperties();
            response.ApplicationProperties.Map[CbsConstants.PutToken.StatusCode] = (int)statusCode;
            response.ApplicationProperties.Map[CbsConstants.PutToken.StatusDescription] = statusDescription;
            return response;
        }

        async void OnMessageReceived(AmqpMessage message)
        {
            Events.NewTokenReceived();
            try
            {
                await this.HandleTokenUpdate(message);
            }
            catch (Exception ex)
            {
                Events.ErrorHandlingTokenUpdate(ex);
            }
        }

        async Task HandleTokenUpdate(AmqpMessage message)
        {
            try
            {
                (AmqpResponseStatusCode statusCode, string description) = await this.UpdateCbsToken(message);
                await this.SendResponseAsync(message, statusCode, description);
            }
            catch (Exception e)
            {
                await this.SendResponseAsync(message, AmqpResponseStatusCode.InternalServerError, e.Message);
                Events.ErrorUpdatingToken(e);
            }
        }

        Task SendResponseAsync(AmqpMessage requestMessage, AmqpResponseStatusCode statusCode, string statusDescription)
        {
            try
            {
                AmqpMessage response = GetAmqpResponse(requestMessage, statusCode, statusDescription);
                return this.sendingLink?.SendMessageAsync(response, this.GetDeliveryTag(), AmqpConstants.NullBinary, AmqpConstants.DefaultTimeout) ?? Task.CompletedTask;
            }
            catch (Exception e)
            {
                Events.ErrorSendingResponse(e);
                return Task.CompletedTask;
            }
        }

        internal class ClientToken
        {
            public ClientToken(string deviceId, Option<string> moduleId, string token)
            {
                this.DeviceId = deviceId;
                this.ModuleId = moduleId;
                this.Token = token;
            }

            public string Id => this.ModuleId.Map(m => $"{this.DeviceId}/{m}").GetOrElse(this.DeviceId);

            public string DeviceId { get; }

            public Option<string> ModuleId { get; }

            public string Token { get; }
        }

        static class Events
        {
            const int IdStart = AmqpEventIds.CbsNode;
            static readonly ILogger Log = Logger.Factory.CreateLogger<CbsNode>();

            enum EventIds
            {
                LinkRegistered = IdStart,
                TokenReceived,
                ErrorUpdatingToken,
                ErrorGettingIdentity,
                TokenUpdated,
                ErrorSendingResponse,
                ErrorHandlingTokenUpdate,
                CbsTokenNotUpdated
            }

            public static void LinkRegistered(IAmqpLink link)
            {
                Log.LogDebug((int)EventIds.LinkRegistered, "Cbs {0} link registered".FormatInvariant(link.IsReceiver ? "receiver" : "sender"));
            }

            public static void NewTokenReceived()
            {
                Log.LogInformation((int)EventIds.TokenReceived, "New token received on the Cbs link");
            }

            public static void ErrorUpdatingToken(Exception exception)
            {
                Log.LogWarning((int)EventIds.ErrorUpdatingToken, exception, "Error updating token received on the Cbs link");
            }

            public static void ErrorGettingIdentity(Exception exception)
            {
                Log.LogWarning((int)EventIds.ErrorGettingIdentity, exception, "Error getting identity from the token received on the Cbs link");
            }

            public static void ErrorAuthenticatingIdentity(string id, Exception e)
            {
                Log.LogWarning((int)EventIds.ErrorGettingIdentity, e, $"Error authenticating token received on the Cbs link for {id}");
            }

            public static void CbsTokenUpdated(string id)
            {
                Log.LogInformation((int)EventIds.TokenUpdated, $"Token updated for {id}");
            }

            public static void ErrorSendingResponse(Exception exception)
            {
                Log.LogWarning((int)EventIds.ErrorSendingResponse, exception, "Error sending response message");
            }

            public static void ErrorHandlingTokenUpdate(Exception exception)
            {
                Log.LogWarning((int)EventIds.ErrorHandlingTokenUpdate, exception, "Error handling token update");
            }

            public static void CbsTokenNotUpdated(string id)
            {
                Log.LogDebug((int)EventIds.CbsTokenNotUpdated, $"Got a new token for an unauthenticated identity {id}, not updating credentials cache");
            }
        }
    }
}
