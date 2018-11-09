// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Edge.Hub.Amqp
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.Azure.Amqp;
    using Microsoft.Azure.Amqp.Framing;
    using Microsoft.Azure.Devices.Common;
    using Microsoft.Azure.Devices.Common.Security;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Concurrency;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// This class is used to get tokens from the Client on the CBS link. It generates
    /// an identity from the received token and authenticates it.
    /// </summary>
    class CbsNode : ICbsNode
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
        bool disposed;
        AmqpAuthentication amqpAuthentication;
        Task<AmqpAuthentication> authenticationUpdateTask;
        ISendingAmqpLink sendingLink;
        IReceivingAmqpLink receivingLink;
        int deliveryCount;

        public CbsNode(IClientCredentialsFactory identityFactory, string iotHubHostName, IAuthenticator authenticator, ICredentialsCache credentialsCache)
        {
            this.clientCredentialsFactory = identityFactory;
            this.iotHubHostName = iotHubHostName;
            this.authenticator = authenticator;
            this.credentialsCache = credentialsCache;
            this.authenticationUpdateTask = Task.FromResult(AmqpAuthentication.Unauthenticated);
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        public Task<AmqpAuthentication> GetAmqpAuthentication() => this.authenticationUpdateTask;

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

        internal static (string deviceId, string moduleId) ParseIds(string audience)
        {
            string audienceUri = audience.StartsWith("amqps://", StringComparison.CurrentCultureIgnoreCase) ? audience : "amqps://" + audience;

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

        internal IClientCredentials GetClientCredentials(AmqpMessage message)
        {
            (string token, string audience) = ValidateAndParseMessage(this.iotHubHostName, message);
            (string deviceId, string moduleId) = ParseIds(audience);
            IClientCredentials clientCredentials = this.clientCredentialsFactory.GetWithSasToken(deviceId, moduleId, string.Empty, token, true);
            return clientCredentials;
        }

        internal ArraySegment<byte> GetDeliveryTag()
        {
            int deliveryId = Interlocked.Increment(ref this.deliveryCount);
            return new ArraySegment<byte>(BitConverter.GetBytes(deliveryId));
        }

        // Note: This method accesses this.amqpAuthentication, and should be invoked only within this.identitySyncLock
        internal async Task<(AmqpAuthentication, AmqpResponseStatusCode, string)> UpdateCbsToken(AmqpMessage message)
        {
            IClientCredentials identity;
            try
            {
                identity = this.GetClientCredentials(message);
            }
            catch (Exception e) when (!ExceptionEx.IsFatal(e))
            {
                Events.ErrorGettingIdentity(e);
                return (AmqpAuthentication.Unauthenticated, AmqpResponseStatusCode.BadRequest, e.Message);
            }

            if ((this.amqpAuthentication == null || !this.amqpAuthentication.IsAuthenticated)
                && !await this.authenticator.AuthenticateAsync(identity))
            {
                Events.ErrorAuthenticatingIdentity(identity.Identity);
                return (AmqpAuthentication.Unauthenticated, AmqpResponseStatusCode.BadRequest, $"Unable to authenticate {identity.Identity.Id}");
            }
            else
            {
                Events.CbsTokenUpdated(identity.Identity);
                await this.credentialsCache.Add(identity);
                return (new AmqpAuthentication(true, Option.Some(identity)), AmqpResponseStatusCode.OK, AmqpResponseStatusCode.OK.ToString());
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposed)
            {
                this.disposed = true;
            }
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

        void OnMessageReceived(AmqpMessage message)
        {
            Events.NewTokenReceived();
            this.authenticationUpdateTask = this.UpdateAmqpAuthentication(message);
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

        async Task<AmqpAuthentication> UpdateAmqpAuthentication(AmqpMessage message)
        {
            using (await this.identitySyncLock.LockAsync())
            {
                try
                {
                    (AmqpAuthentication amqpAuth, AmqpResponseStatusCode statusCode, string description) = await this.UpdateCbsToken(message);
                    await this.SendResponseAsync(message, statusCode, description);
                    this.amqpAuthentication = amqpAuth;
                    return this.amqpAuthentication;
                }
                catch (Exception e)
                {
                    await this.SendResponseAsync(message, AmqpResponseStatusCode.InternalServerError, e.Message);
                    Events.ErrorUpdatingToken(e);
                    return AmqpAuthentication.Unauthenticated;
                }
            }
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
                ErrorSendingResponse
            }

            public static void CbsTokenUpdated(IIdentity identity)
            {
                Log.LogInformation((int)EventIds.TokenUpdated, $"Token updated for {identity.Id}");
            }

            public static void ErrorAuthenticatingIdentity(IIdentity identity)
            {
                Log.LogWarning((int)EventIds.ErrorGettingIdentity, $"Error authenticating token received on the Cbs link for {identity.Id}");
            }

            public static void ErrorGettingIdentity(Exception exception)
            {
                Log.LogWarning((int)EventIds.ErrorGettingIdentity, exception, "Error getting identity from the token received on the Cbs link");
            }

            public static void ErrorSendingResponse(Exception exception)
            {
                Log.LogWarning((int)EventIds.ErrorSendingResponse, exception, "Error sending response message");
            }

            public static void ErrorUpdatingToken(Exception exception)
            {
                Log.LogWarning((int)EventIds.ErrorUpdatingToken, exception, "Error updating token received on the Cbs link");
            }

            public static void LinkRegistered(IAmqpLink link)
            {
                Log.LogDebug((int)EventIds.LinkRegistered, "Cbs {0} link registered".FormatInvariant(link.IsReceiver ? "receiver" : "sender"));
            }

            public static void NewTokenReceived()
            {
                Log.LogInformation((int)EventIds.TokenReceived, "New token received on the Cbs link");
            }
        }
    }
}
