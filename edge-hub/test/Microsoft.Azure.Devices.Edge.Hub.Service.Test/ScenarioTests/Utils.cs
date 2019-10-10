namespace Microsoft.Azure.Devices.Edge.Hub.Service.Test.ScenarioTests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    internal static class ExtensionUtils
    {
        internal static void EnsureOrdered(this IReadOnlyList<Hub.Core.IMessage> messages)
        {
            var deliveryOrder = messages.Select(m => Convert.ToInt32(m.Properties["counter"])).ToList();

            for (var i = 0; i < deliveryOrder.Count - 1; i++)
            {
                if (deliveryOrder[i] != deliveryOrder[i + 1] - 1)
                {
                    var toThrow = new Exception("Messages are not ordered");
                    toThrow.Data["msg1"] = messages[i];
                    toThrow.Data["msg2"] = messages[i + 1];

                    throw toThrow;
                }
            }
        }
    }
}
