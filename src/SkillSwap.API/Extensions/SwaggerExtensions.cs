using Microsoft.OpenApi;

namespace SkillSwap.API.Extensions;

/// <summary>
/// Swagger/OpenAPI configuration including JWT Bearer security support.
/// Compatible with Swashbuckle.AspNetCore 10.x and Microsoft.OpenApi 2.x.
/// </summary>
public static class SwaggerExtensions
{
    public static IServiceCollection AddSkillSwapSwagger(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "SkillSwap API",
                Version = "v1",
                Description = "Peer-to-peer skill exchange and time-bank platform API.",
                Contact = new OpenApiContact
                {
                    Name = "SkillSwap Team"
                }
            });

            // JWT Bearer security definition
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Description = "JWT Authorization header using the Bearer scheme. " +
                              "Enter 'Bearer' [space] and then your token in the text input below.",
                Name = "Authorization",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.ApiKey,
                Scheme = "Bearer",
                BearerFormat = "JWT"
            });

            // Require the Bearer token globally (Swashbuckle 10.x uses a Func<OpenApiDocument, OpenApiSecurityRequirement>)
            options.AddSecurityRequirement(_ => new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecuritySchemeReference("Bearer"),
                    new List<string>()
                }
            });
        });

        return services;
    }

    public static IApplicationBuilder UseSkillSwapSwagger(this IApplicationBuilder app)
    {
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "SkillSwap API v1");
            options.DisplayRequestDuration();
        });
        return app;
    }
}
