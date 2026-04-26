using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace EList.Filestorage.Api.Infrastructure
{
    /// <summary>
    /// Фильтр добавления визуального отображения необходимости указывать токен авторизации
    /// </summary>
    public class BasicAuthenticationSecuritySchemeFilter : IOperationFilter
    {
        /// <summary>
        /// Метод применения фильтра
        /// </summary>
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            if (!context.MethodInfo.GetCustomAttributes(true).Any(x => x is AllowAnonymousAttribute) &&
            !context.MethodInfo.DeclaringType.GetCustomAttributes(true).Any(x => x is AllowAnonymousAttribute))
            {
                operation.Security = new List<OpenApiSecurityRequirement>
                {
                    new OpenApiSecurityRequirement
                    {
                        {
                            GetOpenApiSecurityScheme(), new string[] { }
                        }
                    }
                };
            }
        }
        /// <summary>
        /// Метод получения схемы для добавления кнопки ввода Authorization параметра
        /// </summary>
        public static OpenApiSecurityScheme GetOpenApiSecurityScheme()
        {
            const string Scheme = "EList";
            return new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.ApiKey,
                In = ParameterLocation.Header,
                Description = string.Format("JWT Authorization header using the Bearer scheme. Example: \"Authorization: {0} {1}\"", Scheme, "{token}"),
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = Scheme
                }
            };
        }
    }
}
