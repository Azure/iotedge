// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Billing
{
    using Microsoft.Azure.Devices.Edge.Util;
    using System;

    public class SynchedPurchase
    {
        public SynchedPurchase(DateTime synchedDateUtc) : this(synchedDateUtc, Option.None<PurchaseContent>())
        {
        }

        public SynchedPurchase(DateTime synchedDateUtc, Option<PurchaseContent> purchaseContent)
        {
            this.SynchedDateUtc = synchedDateUtc;
            this.PurchaseContent = purchaseContent;
        }

        public Option<PurchaseContent> PurchaseContent { get; set; }

        public DateTime SynchedDateUtc { get; set; }
    }
}
