// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Routing.Core.Query
{
    using System.Linq.Expressions;

    using Antlr4.Runtime;

    using Microsoft.Azure.Devices.Routing.Core.MessageSources;

    public interface IBuiltin
    {
        bool IsBodyQuery { get; }

        Expression Get(IToken token, Expression[] args, Expression[] contextArgs, ErrorListener errors);

        bool IsEnabled(RouteCompilerFlags routeCompilerFlags);

        bool IsValidMessageSource(IMessageSource messageSource);
    }
}
