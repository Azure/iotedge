// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Http.Swagger
{
    using System.Collections.Generic;
    using Swashbuckle.AspNetCore.Swagger;
    using Swashbuckle.AspNetCore.SwaggerGen;

    public class SwaggerDocumentFilter : IDocumentFilter
    {
        public const string ApiVersionQueryStringParameter = "api-version";

        public void Apply(SwaggerDocument swaggerDoc, DocumentFilterContext context)
        {
            swaggerDoc.Schemes = new[] { "https" };

            if (swaggerDoc.Parameters == null)
            {
                swaggerDoc.Parameters = new Dictionary<string, IParameter>();
            }

            swaggerDoc.Parameters.Add(ApiVersionQueryStringParameter, new NonBodyParameter()
            {
                Name = ApiVersionQueryStringParameter,
                In = "query",
                Required = true,
                Type = "string",
                Default = HttpConstants.ApiVersion
            });
        }
    }
}
