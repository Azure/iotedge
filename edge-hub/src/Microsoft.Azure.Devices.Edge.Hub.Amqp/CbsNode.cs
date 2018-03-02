// Copyright (c) Microsoft. All rights reserved.
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
        readonly IIdentityFactory identityFactory;
        readonly IAuthenticator authenticator;
        readonly string iotHubHostName;
        bool disposed;
        Task<AmqpAuthentication> identityUpdateTask;
        ISendingAmqpLink sendingLink;
        IReceivingAmqpLink receivingLink;
        int deliveryCount;

        public CbsNode(IIdentityFactory identityFactory, string iotHubHostName, IAuthenticator authenticator)
        {
            this.identityFactory = identityFactory;
            this.iotHubHostName = iotHubHostName;
            this.authenticator = authenticator;
            this.identityUpdateTask = Task.FromResult(AmqpAuthentication.Unauthenticated);
        }

        public Task<AmqpAuthentication> GetAmqpAuthentication() => this.identityUpdateTask;

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

        void OnMessageReceived(AmqpMessage message)
        {
            Events.NewTokenReceived();
            this.identityUpdateTask = this.UpdateAmqpAuthentication(message);
        }

        async Task<AmqpAuthentication> UpdateAmqpAuthentication(AmqpMessage message)
        {
            try
            {
                (AmqpAuthentication amqpAuthentication, AmqpResponseStatusCode statusCode, string description) = await this.UpdateCbsToken(message);
                await this.SendResponseAsync(message, statusCode, description);
                return amqpAuthentication;
            }
            catch (Exception e)
            {
                await this.SendResponseAsync(message, AmqpResponseStatusCode.InternalServerError, e.Message);
                Events.ErrorUpdatingToken(e);
                return AmqpAuthentication.Unauthenticated;
            }
        }

        internal async Task<(AmqpAuthentication, AmqpResponseStatusCode, string)> UpdateCbsToken(AmqpMessage message)
        {
            using (await this.identitySyncLock.LockAsync())
            {
                IIdentity identity;
                try
                {
                    identity = this.GetIdentity(message);
                }
                catch (Exception e) when (!ExceptionEx.IsFatal(e))
                {
                    Events.ErrorGettingIdentity(e);
                    return (AmqpAuthentication.Unauthenticated, AmqpResponseStatusCode.BadRequest, e.Message);
                }

                if (!await this.authenticator.AuthenticateAsync(identity))
                {
                    Events.ErrorAuthenticatingIdentity(identity);
                    return (AmqpAuthentication.Unauthenticated, AmqpResponseStatusCode.BadRequest, $"Unable to authenticate {identity.Id}");
                }

                Events.CbsTokenUpdated(identity);
                return (new AmqpAuthentication(true, Option.Some(identity)), AmqpResponseStatusCode.OK, AmqpResponseStatusCode.OK.ToString());
            }
        }

        internal IIdentity GetIdentity(AmqpMessage message)
        {
            (string token, string audience) = ValidateAndParseMessage(this.iotHubHostName, message);
            (string deviceId, string moduleId) = ParseIds(audience);
            Try<IIdentity> identity = this.identityFactory.GetWithSasToken(deviceId, moduleId, string.Empty, moduleId != null, token);
            if (!identity.Success)
            {
                throw identity.Exception;
            }
            return identity.Value;
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

        static AmqpMessage GetAmqpResponse(AmqpMessage requestMessage, AmqpResponseStatusCode statusCode, string statusDescription)
        {
            AmqpMessage response = AmqpMessage.Create();
            response.Properties.CorrelationId = requestMessage.Properties.MessageId;
            response.ApplicationProperties = new ApplicationProperties();
            response.ApplicationProperties.Map[CbsConstants.PutToken.StatusCode] = (int)statusCode;
            response.ApplicationProperties.Map[CbsConstants.PutToken.StatusDescription] = statusDescription;
            return response;
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

        internal ArraySegment<byte> GetDeliveryTag()
        {
            int deliveryId = Interlocked.Increment(ref this.deliveryCount);
            return new ArraySegment<byte>(BitConverter.GetBytes(deliveryId));
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

        static class Events
        {
            static readonly ILogger Log = Logger.Factory.CreateLogger<CbsNode>();
            const int IdStart = AmqpEventIds.CbsNode;

            enum EventIds
            {
                LinkRegistered = IdStart,
                TokenReceived,
                ErrorUpdatingToken,
                ErrorGettingIdentity,
                TokenUpdated,
                ErrorSendingResponse
            }

            public static void LinkRegistered(IAmqpLink link)
            {
                Log.LogDebug((int)EventIds.LinkRegistered, "Cbs {0} link registered".FormatInvariant(link.IsReceiver ? "receiver" : "sender"));
            }

            public static void NewTokenReceived()
            {
                Log.LogDebug((int)EventIds.TokenReceived, "New token received on the Cbs link");
            }

            public static void ErrorUpdatingToken(Exception exception)
            {
                Log.LogWarning((int)EventIds.ErrorUpdatingToken, exception, "Error updating token received on the Cbs link");
            }

            public static void ErrorGettingIdentity(Exception exception)
            {
                Log.LogWarning((int)EventIds.ErrorGettingIdentity, exception, "Error getting identity from the token received on the Cbs link");
            }

            public static void ErrorAuthenticatingIdentity(IIdentity identity)
            {
                Log.LogWarning((int)EventIds.ErrorGettingIdentity, $"Error authenticating token received on the Cbs link for {identity.Id}");
            }

            public static void CbsTokenUpdated(IIdentity identity)
            {
                Log.LogDebug((int)EventIds.TokenUpdated, $"Token updated for {identity.Id}");
            }

            public static void ErrorSendingResponse(Exception exception)
            {
                Log.LogWarning((int)EventIds.ErrorSendingResponse, exception, "Error sending response message");
            }
        }
    }
}
