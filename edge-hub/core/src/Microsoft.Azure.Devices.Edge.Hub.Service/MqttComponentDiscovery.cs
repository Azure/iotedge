// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Autofac;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    public class MqttBridgeComponentDiscovery : IComponentDiscovery
    {
        readonly ILogger logger;

        public IReadOnlyCollection<ISubscriber> Subscribers { get; private set; }
        public IReadOnlyCollection<ISubscriptionWatcher> SubscriptionWatchers { get; private set; }
        public IReadOnlyCollection<IMessageProducer> Producers { get; private set; }
        public IReadOnlyCollection<IMessageConsumer> Consumers { get; private set; }

        public static Type[] CandidateInterfaces { get; } = new[] { typeof(ISubscriber), typeof(ISubscriptionWatcher), typeof(IMessageProducer), typeof(IMessageConsumer) };

        public MqttBridgeComponentDiscovery(ILogger logger)
        {
            this.logger = Preconditions.CheckNotNull(logger);

            this.Subscribers = new ISubscriber[0];
            this.SubscriptionWatchers = new ISubscriptionWatcher[0];
            this.Producers = new IMessageProducer[0];
            this.Consumers = new IMessageConsumer[0];
        }

        public void Discover(IComponentContext context)
        {
            var componentInstances = GetCandidateTypes().Select(t => context.Resolve(t));

            var subscribers = new List<ISubscriber>();
            var subscriptionWatchers = new List<ISubscriptionWatcher>();
            var producers = new List<IMessageProducer>();
            var consumers = new List<IMessageConsumer>();

            foreach (var component in componentInstances)
            {
                if (component is ISubscriber subscriber)
                {
                    subscribers.Add(subscriber);
                    this.logger.LogDebug("Added class [{0}] as subscriber", subscriber.GetType().Name);
                }

                if (component is ISubscriptionWatcher subscriptionWatcher)
                {
                    subscriptionWatchers.Add(subscriptionWatcher);
                    this.logger.LogDebug("Added class [{0}] as subscription watcher", subscriptionWatcher.GetType().Name);
                }

                if (component is IMessageProducer producer)
                {
                    producers.Add(producer);
                    this.logger.LogDebug("Added class [{0}] as producer", producer.GetType().Name);
                }

                if (component is IMessageConsumer consumer)
                {
                    consumers.Add(consumer);
                    this.logger.LogDebug("Added class [{0}] as consumer", consumer.GetType().Name);
                }
            }

            this.Subscribers = subscribers;
            this.SubscriptionWatchers = subscriptionWatchers;
            this.Producers = producers;
            this.Consumers = consumers;
        }

        public static IEnumerable<Type> GetCandidateTypes()
        {
            var candidateTypes = AppDomain.CurrentDomain
                                          .GetAssemblies()
                                          .SelectMany(a => a.GetTypes())
                                          .Where(x => CandidateInterfaces.Any(i => i.IsAssignableFrom(x) && !x.IsInterface && !x.IsAbstract));

            return candidateTypes;
        }
    }
}
