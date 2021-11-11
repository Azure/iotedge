// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Billing
{
    using System;
    using Microsoft.Azure.Devices.Edge.Util;

    public class SynchedPurchase
    {
        public SynchedPurchase(DateTime synchedDateUtc)
            : this(synchedDateUtc, Option.None<PurchaseContent>())
        {
        }

        public SynchedPurchase(DateTime synchedDateUtc, Option<PurchaseContent> purchaseContent)
        {
            this.SynchedDateUtc = synchedDateUtc;
            this.PurchaseContent = purchaseContent;
        }

        public Option<PurchaseContent> PurchaseContent { get; }

        public DateTime SynchedDateUtc { get; }
    }
}
