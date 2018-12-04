// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Routing.Core.Query
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using Microsoft.Azure.Devices.Routing.Core.Query.Errors;
    using Microsoft.Azure.Devices.Routing.Core.Util;

    public class RouteCompilationException : Exception
    {
        public IList<CompilationError> Errors { get; }

        public RouteCompilationException(IEnumerable<CompilationError> errors)
        {
            this.Errors = Preconditions.CheckNotNull(errors).ToImmutableList();
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            if (this.Errors != null && this.Errors.Any())
            {
                sb.AppendLine("Compilation errors:");
                foreach (string error in this.Errors.Select(
                    e => string.Format(CultureInfo.InvariantCulture, "StartLine[{0}], StartColumn[{1}], EndLine[{2}], EndColumn[{3}], Severity[{4}], Message: {5}",
                        e.Location.Start.Line,
                        e.Location.Start.Column,
                        e.Location.End.Line,
                        e.Location.End.Column,
                        e.Severity,
                        e.Message)))
                {
                    sb.AppendLine(error);
                }
            }

            sb.AppendLine(base.ToString());
            return sb.ToString();
        }
    }
}
