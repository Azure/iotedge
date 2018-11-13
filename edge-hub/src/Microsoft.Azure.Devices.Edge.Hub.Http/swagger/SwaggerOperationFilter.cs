// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Http.Swagger
{
    using System.Collections.Generic;
    using System.Linq;
    using Newtonsoft.Json;
    using Swashbuckle.AspNetCore.Swagger;
    using Swashbuckle.AspNetCore.SwaggerGen;

    public class SwaggerOperationFilter : IOperationFilter
    {
        public void Apply(Operation operation, OperationFilterContext context)
        {
            TrimContentTypes(operation.Consumes);
            TrimContentTypes(operation.Produces);

            if (operation.Parameters != null && operation.Parameters.Any())
            {
                this.SetBodyParametersAsRequired(operation);
            }

            this.SetApiVersionQueryParameter(operation);
        }

        void SetBodyParametersAsRequired(Operation operation)
        {
            IEnumerable<IParameter> bodyParameters = operation.Parameters.Where(p => p.In == "body");

            foreach (IParameter bodyParameter in bodyParameters)
            {
                bodyParameter.Required = true;
            }
        }

        void SetApiVersionQueryParameter(Operation operation)
        {
            if (operation.Parameters == null)
            {
                operation.Parameters = new List<IParameter>();
            }

            operation.Parameters.Add(new RefNonBodyParameter()
            {
                Ref = "#/parameters/" + SwaggerDocumentFilter.ApiVersionQueryStringParameter
            });
        }

        static void TrimContentTypes(IList<string> contextTypes)
        {
            if (contextTypes.Any())
            {
                contextTypes.Clear();
                contextTypes.Add("application/json");
            }
        }

        public class RefNonBodyParameter : PartialSchema, IParameter
        {
            [JsonProperty("$ref")]
            public string Ref { get; set; }

            public string Name { get; set; }

            public string In { get; set; }

            public string Description { get; set; }

            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
            public bool Required { get; set; }
        }
    }
}
