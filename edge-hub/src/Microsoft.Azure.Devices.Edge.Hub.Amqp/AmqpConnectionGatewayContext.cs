// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Amqp
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using Microsoft.Azure.Amqp;
    using Microsoft.Azure.Devices.Edge.Util;

    public sealed class AmqpConnectionGatewayContext
    {
        readonly object syncLock;
        readonly Dictionary<Type, Collection<AmqpLink>> amqpLinks;

        // TODO: Remove suppression once implementation is complete.
        // ReSharper disable once NotAccessedField.Local
        readonly AmqpConnection amqpConnection;

        public AmqpConnectionGatewayContext(AmqpConnection amqpConnection)
        {
            this.amqpConnection = amqpConnection;
            this.amqpLinks = new Dictionary<Type, Collection<AmqpLink>>();
            this.syncLock = new object();
        }

        public void AddLink(AmqpLink amqpLink)
        {
            lock (this.syncLock)
            {
                if (this.amqpLinks.TryGetValue(amqpLink.GetType(), out Collection<AmqpLink> amqpCollection))
                {
                    amqpCollection.Add(amqpLink);
                }
                else
                {
                    amqpCollection = new Collection<AmqpLink>
                    {
                        amqpLink
                    };
                    this.amqpLinks.Add(amqpLink.GetType(), amqpCollection);
                }
            }
        }

        public bool TryRemoveLink(AmqpLink amqpLink)
        {
            lock (this.syncLock)
            {
                if (this.amqpLinks.TryGetValue(amqpLink.GetType(), out Collection<AmqpLink> amqpCollection))
                {
                    amqpCollection.Remove(amqpLink);
                    if (amqpCollection.Count == 0)
                    {
                        return this.amqpLinks.TryRemove(amqpLink.GetType(), out amqpCollection);
                    }
                }
            }

            return true;
        }

        public bool TryFindLink(Type type, out AmqpLink amqpLink)
        {
            amqpLink = null;
            lock (this.syncLock)
            {
                if (this.amqpLinks.TryGetValue(type, out Collection<AmqpLink> amqpCollection))
                {
                    amqpLink = amqpCollection?.First();
                    return true;
                }
            }

            return false;
        }

        public Collection<AmqpLink> FindLinks(Type type)
        {
            lock (this.syncLock)
            {
                if (this.amqpLinks.TryGetValue(type, out Collection<AmqpLink> amqpCollection))
                {
                    return amqpCollection;
                }
            }

            return null;
        }
    }
}
