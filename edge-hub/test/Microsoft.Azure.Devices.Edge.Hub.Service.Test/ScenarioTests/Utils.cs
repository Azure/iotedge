// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Service.Test.ScenarioTests
{
    using System;
    using System.Collections.Generic;
    using System.Dynamic;
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;

    internal static class Utils
    {
        internal static CancellationToken CancelAfter(TimeSpan cancelAfter)
        {
            var tokenSource = new CancellationTokenSource();
            tokenSource.CancelAfter(cancelAfter);

            return tokenSource.Token;
        }

        internal static async Task WaitForAsync(Func<bool> condition, CancellationToken cancellationToken)
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                Thread.MemoryBarrier(); // in case 'condition' doesn't have volatile read
                if (condition())
                {
                    return;
                }

                await Task.Delay(500);
            }
        }

        internal static string RandomString(Random random, int length)
        {
            const string chars = "abcdefghijklmnopqrstuvwxyz0123456789";
            return new string(Enumerable.Repeat(chars, length)
              .Select(s => s[random.Next(s.Length)]).ToArray());
        }
    }

    internal class TestMilestone : IDisposable
    {
        private SemaphoreSlim semaphore = new SemaphoreSlim(0, 1);
        private int passed = 0;
        private int awaited = 0;

        public bool IsPassed => Volatile.Read(ref this.passed) == 1;

        public void Passed()
        {
            // only the first pass matters and awaitable
            if (Interlocked.Exchange(ref this.passed, 1) == 0)
            {
                this.semaphore.Release();
            }

            return;
        }

        public Task WaitAsync()
        {
            if (Volatile.Read(ref this.passed) == 1)
            {
                return Task.CompletedTask;
            }

            // this class is designed for simple linear scenarios, and doesn't support
            // multiple calls to await - just wrapped the semaphore to make the test code prettier.
            if (Interlocked.CompareExchange(ref this.awaited, 1, 0) == 1)
            {
                throw new InvalidOperationException("TestMilestone is not designed to be await from multiple locations");
            }

            return this.semaphore.WaitAsync();
        }

        public void Dispose()
        {
            this.semaphore.Dispose();
        }
    }

    internal static class ExtensionUtils
    {
        internal static void EnsureOrdered(this IReadOnlyList<Devices.Client.Message> messages)
        {
            var deliveryOrder = messages.Select(m => Convert.ToInt32(m.Properties["counter"])).ToList();
            EnsureOrderedInternal(deliveryOrder, messages);
        }

        internal static void EnsureOrdered(this IReadOnlyList<Hub.Core.IMessage> messages)
        {
            var deliveryOrder = messages.Select(m => Convert.ToInt32(m.Properties["counter"])).ToList();
            EnsureOrderedInternal(deliveryOrder, messages);
        }

        private static void EnsureOrderedInternal(List<int> deliveryOrder, IReadOnlyList<object> messages)
        {
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

        internal static void EnsureOrderedWithGaps(this IReadOnlyList<Devices.Client.Message> messages)
        {
            var deliveryOrder = messages.Select(m => Convert.ToInt32(m.Properties["counter"])).ToList();
            EnsureOrderedWithGapsInternal(deliveryOrder, messages);
        }

        internal static void EnsureOrderedWithGaps(this IReadOnlyList<Hub.Core.IMessage> messages)
        {
            var deliveryOrder = messages.Select(m => Convert.ToInt32(m.Properties["counter"])).ToList();
            EnsureOrderedWithGapsInternal(deliveryOrder, messages);
        }

        private static void EnsureOrderedWithGapsInternal(List<int> deliveryOrder, IReadOnlyList<object> messages)
        {
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

        // there is an asymmetry between Set/Get member implementation just because of the current use cases
        // if you need to get both private properties and fields, expand the logic below. Now it only implements
        // getting fields, not properties
        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            var type = this.target.GetType();
            var fieldInfo = type.GetField(binder.Name, BindingFlags.NonPublic | BindingFlags.Instance);

            if (fieldInfo != null)
            {
                result = fieldInfo.GetValue(this.target);
                return true;
            }
            else
            {
                result = null;
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
