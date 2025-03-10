using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Api.Swagger
{
    public class SwaggerFileOperationFilter : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            var fileParameters = context.ApiDescription.ParameterDescriptions
                .Where(x => x.ModelMetadata?.ModelType == typeof(IFormFile));

            if (fileParameters.Any())
            {
                operation.Parameters?.Clear();

                operation.RequestBody = new OpenApiRequestBody
                {
                    Content = new Dictionary<string, OpenApiMediaType>
                    {
                        ["multipart/form-data"] = new OpenApiMediaType
                        {
                            Schema = new OpenApiSchema
                            {
                                Type = "object",
                                Properties = fileParameters.ToDictionary(
                                    p => p.Name ?? "file",
                                    _ => new OpenApiSchema
                                    {
                                        Description = "PDF file to process",
                                        Type = "string",
                                        Format = "binary"
                                    }
                                ),
                                Required = fileParameters.Select(p => p.Name ?? "file").ToHashSet()
                            }
                        }
                    },
                    Description = "PDF file to process",
                    Required = true
                };
            }
        }
    }
}
