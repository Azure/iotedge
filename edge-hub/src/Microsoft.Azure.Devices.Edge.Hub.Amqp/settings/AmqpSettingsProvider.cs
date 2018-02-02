// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Amqp.Settings
{
    using System.Security.Cryptography.X509Certificates;
    using Microsoft.Azure.Amqp;
    using Microsoft.Azure.Amqp.Sasl;
    using Microsoft.Azure.Amqp.Transport;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;
    using Microsoft.Azure.Devices.Edge.Util;
    using Constants = Microsoft.Azure.Devices.Edge.Hub.Amqp.Constants;

    public static class AmqpSettingsProvider
    {
        public static AmqpSettings GetDefaultAmqpSettings(
            string hostName,
            X509Certificate2 tlsCertificate,
            IAuthenticator authenticator,
            IIdentityFactory identityFactory
        )
        {
            Preconditions.CheckNonWhiteSpace(hostName, nameof(hostName));
            Preconditions.CheckNotNull(tlsCertificate, nameof(tlsCertificate));
            Preconditions.CheckNotNull(authenticator, nameof(authenticator));
            Preconditions.CheckNotNull(identityFactory, nameof(identityFactory));

            var settings = new AmqpSettings()
            {
                AllowAnonymousConnection = true,
                RequireSecureTransport = true,
                RuntimeProvider = new AmqpRuntimeProvider(),
            };

            // Add all transport providers we want to support.
            AddSaslProvider();
            AddAmqpProvider();

            return settings;

//TODO: Remove these warnings being disabling once implementation is done.
#pragma warning disable CS8321 // Local function is declared but never used
            // ReSharper disable once UnusedLocalFunction
            void AddTlsProvider()
#pragma warning restore CS8321 // Local function is declared but never used
            {
                // This TLS provider is used for TLS upgrade scenarios - i.e. the client first connects
                // with a plain TCP connection (no TLS) and then requests TLS in the AMQP protocol header
                // (with protocol ID of 2). In this case AmqpTransportProvider will use this provider to
                // do the upgrade to TLS before handing control to the AmqpTransportProvider.
                //
                // NOTE: Since we *require* TLS before AMQP, not sure when a TLS upgrade scenario would
                //       become relevant.
                //
                // NOTE: Note below that we pass `null` for the underlying transport settings. This is
                //       because "TransportSettings" are not used by "transport providers" to create
                //       "TransportListener" objects. Transport providers are handed in an existing
                //       transport which they use to do their thing. This "null" is a consequence of
                //       the fact that we reuse "TlsTransportSettings" for both setting up 
                //       "TlsTransportProvider" objects and for creating "TlsTransportListener"
                //       objects.
                var tlsProvider = new TlsTransportProvider(new TlsTransportSettings(null, false)
                {
                    TargetHost = hostName,
                    Certificate = tlsCertificate,
                });
                tlsProvider.Versions.Add(Constants.AmqpVersion100);
                settings.TransportProviders.Add(tlsProvider);
            }

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

                // TODO: Enable the following 2 handlers when we add support for CBS auth in Edge Hub.
                //saslProvider.AddHandler(new SaslAnonymousHandler(Constants.ServiceBusCbsSaslMechanismName)); // CBS for SB client
                //saslProvider.AddHandler(new SaslAnonymousHandler()); // CBS for other clients

                // This handler implements SAS key based auth.
                saslProvider.AddHandler(new SaslPlainHandler(new EdgeHubSaslPlainAuthenticator(authenticator, identityFactory)));

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
