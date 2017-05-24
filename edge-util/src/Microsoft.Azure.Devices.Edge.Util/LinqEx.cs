namespace Microsoft.Azure.Devices.Edge.Util
{
    using System;
    using System.Collections.Generic;

    public static class LinqEx
    {
        /// <summary>
        /// Ignores exceptions thrown by preceding linq functions and optionally calls
        /// <paramref name="action"/> passing a reference to the exception that was thrown
        /// if it is not <c>null</c>. Adapted from https://stackoverflow.com/a/14741288/8080.
        /// </summary>
        public static IEnumerable<T> IgnoreExceptions<T, TException>(this IEnumerable<T> src, Action<TException> action = null)
            where TException : Exception
        {
            using (IEnumerator<T> enumerator = src.GetEnumerator())
            {
                bool next = true;
                while (next)
                {
                    try
                    {
                        next = enumerator.MoveNext();
                    }
                    catch (TException ex)
                    {
                        action?.Invoke(ex);
                        continue;
                    }

                    if (next)
                    {
                        yield return enumerator.Current;
                    }
                }
            }
        }
    }
}
