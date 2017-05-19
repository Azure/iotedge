// ---------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ---------------------------------------------------------------

namespace Microsoft.Azure.Devices.Routing.Core.Query
{
    using System.Linq.Expressions;
    using Antlr4.Runtime;
    using Microsoft.Azure.Devices.Routing.Core.MessageSources;

    public interface IBuiltin
    {
        bool IsBodyQuery { get; }

        bool IsValidMessageSource(IMessageSource messageSource);

        bool IsEnabled(RouteCompilerFlags routeCompilerFlags);

        Expression Get(IToken token, Expression[] args, Expression[] contextArgs, ErrorListener errors);
    }
}