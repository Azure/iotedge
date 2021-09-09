// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter.Test
{
    using System.Dynamic;
    using System.Reflection;

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
                             type.GetMethod(binder.Name); // sometimes we call public method on non-public class

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

        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            var type = this.target.GetType();
            var fieldInfo = type.GetField(binder.Name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);

            if (fieldInfo != null)
            {
                result = fieldInfo.GetValue(this.target);
                return true;
            }
            else
            {
                // try with properties
                var propertyInfo = type.GetProperty(binder.Name, BindingFlags.NonPublic | BindingFlags.Instance) ??
                                   type.GetProperty(binder.Name);

                if (propertyInfo != null)
                {
                    result = propertyInfo.GetValue(this.target);
                    return true;
                }

                result = null;
                return false;
            }
        }
    }

    internal static class DynamicPrivateAccessor
    {
        /// <summary>
        /// This is for test scenarios only! The purpose of this method is to make it possible to set up
        /// or read an object state for testing purposes. Never use this for production code!
        /// </summary>
        internal static dynamic AsPrivateAccessible(this object target) => new PrivateAccessor(target);
    }
}
