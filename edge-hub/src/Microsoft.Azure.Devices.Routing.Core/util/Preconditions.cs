// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Routing.Core.Util
{
    using System;

    class Preconditions
    {
        /// <summary>
        /// Throws ArgumentException if the bool expression is false.
        /// </summary>
        /// <param name="expression">Expression</param>
        public static void CheckArgument(bool expression)
        {
            if (!expression)
            {
                throw new ArgumentException();
            }
        }

        /// <summary>
        /// Throws ArgumentException if the bool expression is false.
        /// </summary>
        /// <param name="expression">Expression</param>
        /// <param name="message">Message</param>
        public static void CheckArgument(bool expression, string message)
        {
            if (!expression)
            {
                throw new ArgumentException(message);
            }
        }

        /// <summary>
        /// Checks that a reference isn't null. Throws ArgumentNullException if null.
        /// </summary>
        /// <typeparam name="T">Type</typeparam>
        /// <param name="reference">Reference</param>
        /// <returns>The reference</returns>
        public static T CheckNotNull<T>(T reference) => CheckNotNull(reference, string.Empty, string.Empty);

        /// <summary>
        /// Checks that a reference isn't null. Throws ArgumentNullException if null.
        /// </summary>
        /// <typeparam name="T">Type</typeparam>
        /// <param name="reference">Reference</param>
        /// <param name="paramName">Parameter name</param>
        /// <returns>The reference</returns>
        public static T CheckNotNull<T>(T reference, string paramName) => CheckNotNull(reference, paramName, string.Empty);

        /// <summary>
        /// Checks that a reference isn't null. Throws ArgumentNullException if null.
        /// </summary>
        /// <typeparam name="T">Type</typeparam>
        /// <param name="reference">Reference</param>
        /// <param name="paramName">Parameter Name</param>
        /// <param name="message">Message</param>
        /// <returns>The reference</returns>
        public static T CheckNotNull<T>(T reference, string paramName, string message)
        {
            if (reference == null)
            {
                if (string.IsNullOrEmpty(paramName))
                {
                    throw new ArgumentNullException();
                }
                else
                {
                    throw string.IsNullOrEmpty(message) ? new ArgumentNullException(paramName) : new ArgumentNullException(paramName, message);
                }
            }

            return reference;
        }

        /// <summary>
        /// This checks that the item is greater than or equal to the low value.
        /// </summary>
        /// <typeparam name="T">Type</typeparam>
        /// <param name="item">Item to check.</param>
        /// <param name="low">Inclusive low value.</param>
        /// <returns>Given type</returns>
        public static T CheckRange<T>(T item, T low)
            where T : IComparable<T> =>
            CheckRange(item, low, nameof(item));

        /// <summary>
        /// This checks that the item is greater than or equal to the low value.
        /// </summary>
        /// <typeparam name="T">Type</typeparam>
        /// <param name="item">Item to check.</param>
        /// <param name="low">Inclusive low value.</param>
        /// <param name="paramName">Parameter name</param>
        /// <returns>Given type</returns>
        public static T CheckRange<T>(T item, T low, string paramName)
            where T : IComparable<T> =>
            CheckRange(item, low, paramName, string.Empty);

        /// <summary>
        /// This checks that the item is greater than or equal to the low value.
        /// </summary>
        /// <typeparam name="T">Type</typeparam>
        /// <param name="item">Item to check.</param>
        /// <param name="low">Inclusive low value.</param>
        /// <param name="paramName">Parameter name</param>
        /// <param name="message">Message</param>
        /// <returns>Given type</returns>
        public static T CheckRange<T>(T item, T low, string paramName, string message)
            where T : IComparable<T>
        {
            if (item.CompareTo(low) < 0)
            {
                throw new ArgumentOutOfRangeException(paramName, item, message);
            }

            return item;
        }

        /// <summary>
        /// This checks that the item is in the range [low, high).
        /// Throws ArgumentOutOfRangeException if out of range.
        /// </summary>
        /// <typeparam name="T">Type</typeparam>
        /// <param name="item">Item to check.</param>
        /// <param name="low">Inclusive low value.</param>
        /// <param name="high">Exclusive high value</param>
        /// <returns>Given type</returns>
        public static T CheckRange<T>(T item, T low, T high)
            where T : IComparable<T> =>
            CheckRange(item, low, high, nameof(item));

        /// <summary>
        /// This checks that the item is in the range [low, high).
        /// Throws ArgumentOutOfRangeException if out of range.
        /// </summary>
        /// <typeparam name="T">Type</typeparam>
        /// <param name="item">Item to check.</param>
        /// <param name="low">Inclusive low value.</param>
        /// <param name="high">Exclusive high value</param>
        /// <param name="paramName">Paramter name</param>
        /// <returns>Given type</returns>
        public static T CheckRange<T>(T item, T low, T high, string paramName)
            where T : IComparable<T> =>
            CheckRange(item, low, high, paramName, string.Empty);

        /// <summary>
        /// This checks that the item is in the range [low, high).
        /// Throws ArgumentOutOfRangeException if out of range.
        /// </summary>
        /// <typeparam name="T">Type</typeparam>
        /// <param name="item">Item to check.</param>
        /// <param name="low">Inclusive low value.</param>
        /// <param name="high">Exclusive high value</param>
        /// <param name="paramName">Parameter name</param>
        /// <param name="message">Message</param>
        /// <returns>Given type</returns>
        public static T CheckRange<T>(T item, T low, T high, string paramName, string message)
            where T : IComparable<T>
        {
            if (item.CompareTo(low) < 0 || item.CompareTo(high) >= 0)
            {
                throw new ArgumentOutOfRangeException(paramName, item, message);
            }

            return item;
        }
    }
}
