// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Amqp.Settings
{
    using Microsoft.Azure.Amqp;
    using Microsoft.Azure.Amqp.Sasl;
    using Microsoft.Azure.Amqp.Transport;
    using Microsoft.Azure.Devices.Edge.Hub.Amqp.LinkHandlers;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;
    using Constants = Microsoft.Azure.Devices.Edge.Hub.Amqp.Constants;

    public static class AmqpSettingsProvider
    {
        public static AmqpSettings GetDefaultAmqpSettings(
            string iotHubHostName,
            IAuthenticator authenticator,
            IClientCredentialsFactory identityFactory,
            ILinkHandlerProvider linkHandlerProvider,
            IConnectionProvider connectionProvider,
            ICredentialsCache credentialsCache)
        {
            Preconditions.CheckNotNull(authenticator, nameof(authenticator));
            Preconditions.CheckNotNull(identityFactory, nameof(identityFactory));
            Preconditions.CheckNotNull(linkHandlerProvider, nameof(linkHandlerProvider));
            Preconditions.CheckNonWhiteSpace(iotHubHostName, nameof(iotHubHostName));
            Preconditions.CheckNotNull(connectionProvider, nameof(connectionProvider));

            var settings = new AmqpSettings
            {
                AllowAnonymousConnection = true,
                RequireSecureTransport = true,
                RuntimeProvider = new AmqpRuntimeProvider(linkHandlerProvider, true, identityFactory, authenticator, iotHubHostName, connectionProvider, credentialsCache)
            };

            // Add all transport providers we want to support.
            AddSaslProvider();
            AddAmqpProvider();
            return settings;

            void AddSaslProvider()
            {
                var saslProvider = new SaslTransportProvider
                {
                    MaxFrameSize = 65536,
                };
                saslProvider.Versions.Add(Constants.AmqpVersion100);

                // TODO: Verify if this handler still needs to be added. It looks like at some point in the past
                //       SASL EXTERNAL was used to do CBS. Since then we have moved away from that and are using
                //       SASL ANONYMOUS to do CBS. So this may not be needed anymore depending on back-compat
                //       needs (i.e. old clients that are still using EXTERNAL for CBS).
                // saslProvider.AddHandler(new SaslExternalHandler());

                // CBS
                saslProvider.AddHandler(new SaslAnonymousHandler());

                // CBS - used by some SDKs like C
                saslProvider.AddHandler(new SaslAnonymousHandler(Constants.ServiceBusCbsSaslMechanismName));

                // This handler implements SAS key based auth.
                saslProvider.AddHandler(new SaslPlainHandler(new EdgeSaslPlainAuthenticator(authenticator, identityFactory, iotHubHostName)));

                settings.TransportProviders.Add(saslProvider);
            }

            void AddAmqpProvider()
            {
                var amqpProvider = new AmqpTransportProvider();
                amqpProvider.Versions.Add(Constants.AmqpVersion100);
                settings.TransportProviders.Add(amqpProvider);
            }
        }
    }
}
