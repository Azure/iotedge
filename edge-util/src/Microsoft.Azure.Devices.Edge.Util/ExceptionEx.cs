// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util
{
    using System;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.InteropServices;

    public static class ExceptionEx
    {
        public static bool IsFatal(this Exception exception)
        {
            while (exception != null)
            {
                switch (exception)
                {
                    // ReSharper disable once UnusedVariable
                    case OutOfMemoryException ex:
                        return true;
                    // ReSharper disable once UnusedVariable
                    case SEHException ex:
                        return true;
                }

                // These exceptions aren't themselves fatal, but since the CLR uses them to wrap other exceptions,
                // we want to check to see whether they've been used to wrap a fatal exception.  If so, then they
                // count as fatal.
                if (exception is TypeInitializationException || exception is TargetInvocationException)
                {
                    exception = exception.InnerException;
                }
                else if (exception is AggregateException)
                {
                    // AggregateExceptions have a collection of inner exceptions, which may themselves be other
                    // wrapping exceptions (including nested AggregateExceptions).  Recursively walk this
                    // hierarchy.  The (singular) InnerException is included in the collection.
                    ReadOnlyCollection<Exception> innerExceptions = ((AggregateException)exception).InnerExceptions;
                    if (innerExceptions.Any(ex => IsFatal(ex)))
                    {
                        return true;
                    }

                    break;
                }
                else if (exception is NullReferenceException)
                {
                    break;
                }
                else
                {
                    break;
                }
            }

            return false;
        }

        public static T UnwindAs<T>(this Exception exception)
            where T : Exception
        {
            switch (exception)
            {
                case T tException:
                    return tException;
                case AggregateException aggregateException when aggregateException.InnerExceptions.Count == 1:
                    return UnwindAs<T>(aggregateException.InnerException);
                default:
                    return null;
            }
        }

        public static bool HasTimeoutException(this Exception ex) =>
            ex != null &&
            (ex is TimeoutException || HasTimeoutException(ex.InnerException) ||
             (ex is AggregateException argEx && (argEx.InnerExceptions?.Select(e => HasTimeoutException(e)).Any(e => e) ?? false)));
    }
}
