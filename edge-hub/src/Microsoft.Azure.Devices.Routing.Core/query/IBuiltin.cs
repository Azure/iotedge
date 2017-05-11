// ---------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ---------------------------------------------------------------

namespace Microsoft.Azure.Devices.Routing.Core.Query
{
    using System.Linq.Expressions;
    using Antlr4.Runtime;

    public interface IBuiltin
    {
        bool IsBodyQuery { get; }

        bool IsValidMessageSource(MessageSource messageSource);

        bool IsEnabled(RouteCompilerFlags routeCompilerFlags);

        Expression Get(IToken token, Expression[] args, Expression[] contextArgs, ErrorListener errors);
    }
}