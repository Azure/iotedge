// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util
{
    using System;

    public class Preconditions
    {
        /// <summary>
        /// Checks that a reference isn't null. Throws ArgumentNullException if null.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="reference"></param>
        /// <returns>The reference</returns>
        public static T CheckNotNull<T>(T reference) => CheckNotNull(reference, "", "");

        /// <summary>
        /// Checks that a reference isn't null. Throws ArgumentNullException if null.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="reference"></param>
        /// <param name="paramName"></param>
        /// <returns>The reference</returns>
        public static T CheckNotNull<T>(T reference, string paramName) => CheckNotNull(reference, paramName, "");

        /// <summary>
        /// Checks that a reference isn't null. Throws ArgumentNullException if null.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="reference"></param>
        /// <param name="paramName"></param>
        /// <param name="message"></param>
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
        /// Throws ArgumentException if the bool expression is false.
        /// </summary>
        /// <param name="expression"></param>
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
        /// <param name="expression"></param>
        /// <param name="message"></param>
        public static void CheckArgument(bool expression, string message)
        {
            if (!expression)
            {
                throw new ArgumentException(message);
            }
        }


        /// <summary>
        ///  Checks that an Enum is defined. Throws ArgumentOutOfRangeException is not.
        /// </summary>
        /// <typeparam name="T">Enum Type.</typeparam>
        /// <param name="status">Value.</param>
        /// <returns></returns>
        public static T CheckIsDefined<T>(T status)
        {
            Type enumType = typeof(T);
            if (!Enum.IsDefined(enumType, status))
            {
                throw new ArgumentOutOfRangeException(status + " is not a valid value for " + enumType.FullName +  ".");
            }
            return status;
        }


        /// <summary>
        /// This checks that the item is greater than or equal to the low value.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="item">Item to check.</param>
        /// <param name="low">Inclusive low value.</param>
        /// <returns></returns>
        public static T CheckRange<T>(T item, T low) where T : IComparable<T> =>
            CheckRange(item, low, nameof(item));

        /// <summary>
        /// This checks that the item is greater than or equal to the low value.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="item">Item to check.</param>
        /// <param name="low">Inclusive low value.</param>
        /// <param name="paramName"></param>
        /// <returns></returns>
        public static T CheckRange<T>(T item, T low, string paramName) where T : IComparable<T> =>
            CheckRange(item, low, paramName, "");

        /// <summary>
        /// This checks that the item is greater than or equal to the low value.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="item">Item to check.</param>
        /// <param name="low">Inclusive low value.</param>
        /// <param name="paramName"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        public static T CheckRange<T>(T item, T low, string paramName, string message) where T : IComparable<T>
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
        /// <typeparam name="T"></typeparam>
        /// <param name="item">Item to check.</param>
        /// <param name="low">Inclusive low value.</param>
        /// <param name="high">Exclusive high value</param>
        /// <returns></returns>
        public static T CheckRange<T>(T item, T low, T high) where T : IComparable<T> =>
            CheckRange(item, low, high, nameof(item));

        /// <summary>
        /// This checks that the item is in the range [low, high).
        /// Throws ArgumentOutOfRangeException if out of range.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="item">Item to check.</param>
        /// <param name="low">Inclusive low value.</param>
        /// <param name="high">Exclusive high value</param>
        /// <param name="paramName"></param>
        /// <returns></returns>
        public static T CheckRange<T>(T item, T low, T high, string paramName) where T : IComparable<T> =>
            CheckRange(item, low, high, paramName, "");

        /// <summary>
        /// This checks that the item is in the range [low, high).
        /// Throws ArgumentOutOfRangeException if out of range.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="item">Item to check.</param>
        /// <param name="low">Inclusive low value.</param>
        /// <param name="high">Exclusive high value</param>
        /// <param name="paramName"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        public static T CheckRange<T>(T item, T low, T high, string paramName, string message) where T : IComparable<T>
        {
            if (item.CompareTo(low) < 0 || item.CompareTo(high) >= 0)
            {
                throw new ArgumentOutOfRangeException(paramName, item, message);
            }
            return item;
        }

        /// <summary>
        /// Checks if the string is null or whitespace, and throws ArgumentException if it is.
        /// </summary>
        /// <param name="value"></param>
        /// <param name="paramName"></param>
        public static string CheckNonWhiteSpace(string value, string paramName)
        {
            CheckArgument(!string.IsNullOrWhiteSpace(value), $"{paramName} is null or whitespace.");
            return value;
        }

    }
}