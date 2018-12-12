// Copyright (c) Microsoft. All rights reserved.
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

            // Convert exception to EdgeAmqpException
            // TODO: Make sure EdgeAmqpException is thrown from the right places.
            EdgeAmqpException edgeHubAmqpException = GetEdgeHubAmqpException(ex);
            Error amqpError = GenerateAmqpError(edgeHubAmqpException);
            return new AmqpException(amqpError);
        }

        static EdgeAmqpException GetEdgeHubAmqpException(Exception exception)
        {
            if (exception is EdgeAmqpException edgeHubAmqpException)
            {
                return edgeHubAmqpException;
            }
            else if (exception.UnwindAs<UnauthorizedAccessException>() != null)
            {
                return new EdgeAmqpException("Unauthorized access", ErrorCode.IotHubUnauthorizedAccess, exception);
            }
            else if (exception is EdgeHubMessageTooLargeException)
            {
                return new EdgeAmqpException(exception.Message, ErrorCode.MessageTooLarge);
            }
            else if (exception is InvalidOperationException)
            {
                return new EdgeAmqpException("Invalid action performed", ErrorCode.InvalidOperation);
            }
            return new EdgeAmqpException("Encountered server error", ErrorCode.ServerError, exception);
        }

        static Error GenerateAmqpError(EdgeAmqpException exception) => new Error
        {
            Description = JsonConvert.SerializeObject(exception.Message),
            Condition = AmqpErrorMapper.GetErrorCondition(exception.ErrorCode),
            Info = new Fields()
        };
    }
}
