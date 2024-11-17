using System;
using Berg.Api.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Berg.Api.Extensions;

public static class SwaggerExtensions
{
    public static void AddSwagger(this WebApplicationBuilder builder, InfraConfig infraConfig)
    {
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(options =>
        {
            var securityScheme = new OpenApiSecurityScheme
            {
                BearerFormat = "JWT",
                Scheme = "bearer",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.OpenIdConnect,
                OpenIdConnectUrl = new Uri($"https://{infraConfig.ChallengeDomain}/.well-known/openid-configuration"),
                Reference = new OpenApiReference { Id = "oidc", Type = ReferenceType.SecurityScheme }
            };
            options.AddSecurityDefinition("oidc", securityScheme);
            options.AddOperationFilterInstance(new AuthorizationOperationFilter());
            options.SwaggerDoc("v2", new OpenApiInfo {
                Title = "Berg.API v2",
                Version = "v2"
            });
            options.SwaggerDoc("v1", new OpenApiInfo {
                Title = "Berg.API v1",
                Version = "v1"
            });
        });
    }

    public static void UseSwagger(this WebApplication app)
    {
        SwaggerBuilderExtensions.UseSwagger(app);
        app.UseSwaggerUI(options =>
        {
            options.OAuthConfigObject.ClientId = Constants.ClientIds.Berg;
            options.OAuthConfigObject.Scopes = ["openid"];

            // Ugly workaround because swagger-ui doesn't set or validate the 'nonce' parameter for openid requests.
            // See https://github.com/swagger-api/swagger-ui/issues/8315 for details.
            options.OAuthConfigObject.AdditionalQueryStringParams = new Dictionary<string, string>
            {
                { "nonce", "hardcoded" },
                { "prompt", "login" },
            };

            options.SwaggerEndpoint("/swagger/v2/swagger.json", "Berg.API v2");
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "Berg.API v1");
        });
    }

    /// <summary>
    /// Filter that annotates the generated OpenAPI specification with the openid security scheme
    /// if the endpoint is protected with an [Authorize] attribute.
    /// </summary>
    private class AuthorizationOperationFilter : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            var authorizeAttributes = context.MethodInfo.DeclaringType!.GetCustomAttributes(true)
               .Union(context.MethodInfo.GetCustomAttributes(true))
               .OfType<AuthorizeAttribute>()
               .ToList();

            var allowAnonymousAttributes = context.MethodInfo.GetCustomAttributes(true)
                .OfType<AllowAnonymousAttribute>()
                .ToList();

            if (authorizeAttributes.Count == 0 || allowAnonymousAttributes.Count != 0)
               return;

            operation.Responses.TryAdd("200", new OpenApiResponse { Description = "OK" });
            operation.Responses.TryAdd("401", new OpenApiResponse { Description = "Unauthorized" });
            operation.Responses.TryAdd("403", new OpenApiResponse { Description = "Forbidden" });

            operation.Security =
            [
                new()
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Id = "oidc",
                                Type = ReferenceType.SecurityScheme,
                            }
                        }, ["openid"]
                    }
                }
            ];
        }
    }
}
