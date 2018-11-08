// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Edge.Hub.Amqp
{
    using System;

    using Microsoft.Azure.Amqp;
    using Microsoft.Azure.Amqp.Framing;
    using Microsoft.Azure.Devices.Common.Exceptions;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Util;

    using Newtonsoft.Json;

    public class AmqpExceptionsHelper
    {
        public static AmqpException GetAmqpException(Exception ex)
        {
            // If this exception is an AmqpException with LinkRedirect or NotAllowed errors, return it.
            if (ex is AmqpException amqpException)
            {
                if (amqpException.Error.Condition.Equals(AmqpErrorCode.LinkRedirect) || amqpException.Error.Condition.Equals(AmqpErrorCode.NotAllowed))
                {
                    return amqpException;
                }
            }

            // Convert exception to EdgeHubAmqpException
            // TODO: Make sure EdgeHubAmqpException is thrown from the right places.
            EdgeHubAmqpException edgeHubAmqpException = GetEdgeHubAmqpException(ex);
            Error amqpError = GenerateAmqpError(edgeHubAmqpException);
            return new AmqpException(amqpError);
        }

        static Error GenerateAmqpError(EdgeHubAmqpException exception) => new Error
        {
            Description = JsonConvert.SerializeObject(exception.Message),
            Condition = AmqpErrorMapper.GetErrorCondition(exception.ErrorCode),
            Info = new Fields()
        };

        static EdgeHubAmqpException GetEdgeHubAmqpException(Exception exception)
        {
            if (exception is EdgeHubAmqpException edgeHubAmqpException)
            {
                return edgeHubAmqpException;
            }
            else if (exception.UnwindAs<UnauthorizedAccessException>() != null)
            {
                return new EdgeHubAmqpException("Unauthorized access", ErrorCode.IotHubUnauthorizedAccess, exception);
            }
            else if (exception is EdgeHubMessageTooLargeException)
            {
                return new EdgeHubAmqpException(exception.Message, ErrorCode.MessageTooLarge);
            }
            else if (exception is InvalidOperationException)
            {
                return new EdgeHubAmqpException("Invalid action performed", ErrorCode.InvalidOperation);
            }

            return new EdgeHubAmqpException("Encountered server error", ErrorCode.ServerError, exception);
        }
    }
}
