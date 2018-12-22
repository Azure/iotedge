// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.WebJobs.Extensions.EdgeHub.Config
{
    using System;
    using Microsoft.Azure.WebJobs;

    /// <summary>
    /// Extension methods for EdgeHub integration
    /// </summary>
    public static class EdgeHubHostConfigExtensions
    {
        /// <summary>
        /// Adds EdgeHub binding extensions <see cref="IWebJobsBuilder"/>.
        /// </summary>
        /// <param name="builder">The <see cref="IWebJobsBuilder"/> to configure.</param>
        public static IWebJobsBuilder AddEdge(this IWebJobsBuilder builder)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            builder.AddExtension<EdgeHubExtensionConfigProvider>();

            return builder;
        }
    }
}
