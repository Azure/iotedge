// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core.Query.Builtins
{
    using System;
    using System.Globalization;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;
    using Microsoft.Azure.Devices.Routing.Core.Query.JsonPath;
    using Microsoft.Azure.Devices.Routing.Core.Query.Types;
    using Microsoft.Azure.Devices.Routing.Core.Util;
    using Microsoft.Extensions.Logging;

    public class BodyQuery : Builtin
    {
        public override bool IsBodyQuery => true;

        protected override BuiltinExecutor[] Executors => new[]
        {
            new BuiltinExecutor
            {
                InputArgs = new Args(typeof(string)),
                ContextArgs = new Args(typeof(IMessage), typeof(Route)),
                ExecutorFunc = Create
            },
        };

        public override bool IsEnabled(RouteCompilerFlags routeCompilerFlags)
        {
            return routeCompilerFlags.HasFlag(RouteCompilerFlags.BodyQuery);
        }

        static Expression Create(Expression[] args, Expression[] contextArgs)
        {
            var inputExpression = args.FirstOrDefault() as ConstantExpression;
            string queryString = inputExpression?.Value as string;

            if (string.IsNullOrEmpty(queryString))
            {
                throw new ArgumentException("$body query cannot be empty");
            }

            // Validate input string as JsonPath
            if (!JsonPathValidator.IsSupportedJsonPath(queryString, out string errorDetails))
            {
                throw new ArgumentException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Error in $body query. {0}",
                        errorDetails));
            }

            return Expression.Call(typeof(BodyQuery).GetMethod("Runtime", BindingFlags.NonPublic | BindingFlags.Static), args.Union(contextArgs));
        }

        // ReSharper disable once UnusedMember.Local
        static QueryValue Runtime(string queryString, IMessage message, Route route)
        {
            string deviceId = null;
            try
            {
                message.SystemProperties.TryGetValue(SystemProperties.DeviceId, out deviceId);

                QueryValue queryValue = message.GetQueryValue(queryString);

                return queryValue.ValueType == QueryValueType.Object ? QueryValue.Undefined : queryValue;
            }
            catch (Exception ex) when (!ex.IsFatal())
            {
                Events.RuntimeError(route, message, deviceId, ex);
                return QueryValue.Undefined;
            }
        }

        static class Events
        {
            const int IdStart = Routing.EventIds.BodyQuery;
            static readonly ILogger Log = Routing.LoggerFactory.CreateLogger<BodyQuery>();

            enum EventIds
            {
                RuntimeError = IdStart,
            }

            public static void RuntimeError(Route route, IMessage message, string deviceId, Exception ex)
            {
                Log.LogWarning((int)EventIds.RuntimeError, ex, "[RuntimeError] RouteId: '{0}', Condition: '{1}' DeviceId: '{2}'", route.Id, route.Condition, deviceId);
                Routing.UserAnalyticsLogger.LogRouteEvaluationError(message, route, ex);
            }
        }
    }
}
