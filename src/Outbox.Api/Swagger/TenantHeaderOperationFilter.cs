using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Outbox.Api.Swagger
{
    public class TenantHeaderOperationFilter : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            operation.Parameters ??= new List<OpenApiParameter>();

            operation.Parameters.Add(new OpenApiParameter
            {
                Name = "X-Tenant-Id",
                In = ParameterLocation.Header,
                Required = true,
                Description = "Tenant identifier",
                Schema = new OpenApiSchema { Type = "string" }
            });
        }
    }
}