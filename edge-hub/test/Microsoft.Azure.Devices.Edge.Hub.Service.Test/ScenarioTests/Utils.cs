// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Service.Test.ScenarioTests
{
    using System;
    using System.Collections.Generic;
    using System.Dynamic;
    using System.Linq;
    using System.Reflection;

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

        internal static void EnsureOrderedWithGaps(this IReadOnlyList<Hub.Core.IMessage> messages)
        {
            var deliveryOrder = messages.Select(m => Convert.ToInt32(m.Properties["counter"])).ToList();

            for (var i = 0; i < deliveryOrder.Count - 1; i++)
            {
                if (deliveryOrder[i] > deliveryOrder[i + 1])
                {
                    var toThrow = new Exception("Messages are not ordered");
                    toThrow.Data["msg1"] = messages[i];
                    toThrow.Data["msg2"] = messages[i + 1];

                    throw toThrow;
                }
            }
        }
    }

    internal class PrivateAccessor : DynamicObject
    {
        private readonly object target;

        internal PrivateAccessor(object target)
        {
            this.target = target;
        }

        public override bool TryInvokeMember(InvokeMemberBinder binder, object[] args, out object result)
        {
            var type = this.target.GetType();
            var methodInfo = type.GetMethod(binder.Name, BindingFlags.NonPublic | BindingFlags.Instance) ??
                             type.GetMethod(binder.Name);

            if (methodInfo != null)
            {
                result = methodInfo.Invoke(this.target, args);
                return true;
            }
            else
            {
                result = null;
                return false;
            }
        }

        public override bool TrySetMember(SetMemberBinder binder, object value)
        {
            var type = this.target.GetType();
            var propertyInfo = type.GetProperty(binder.Name, BindingFlags.NonPublic | BindingFlags.Instance) ??
                               type.GetProperty(binder.Name); // when getter is public, this returns private setter

            if (propertyInfo != null)
            {
                propertyInfo.SetValue(this.target, value);
                return true;
            }
            else
            {
                return false;
            }
        }
    }

    internal static class DynamicPrivateAccessor
    {
        /// <summary>
        /// This is for test scenarios only! The purpose of this method is to make it possible to set up
        /// an object state for testing purposes. Never use this for production code!
        /// </summary>
        internal static dynamic AsPrivateAccessible(this object target)
        {
            return new PrivateAccessor(target);
        }
    }
}
