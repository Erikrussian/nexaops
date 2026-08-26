using Microsoft.OpenApi.Models;
using NexaOps.Api.Middleware;

var builder = WebApplication.CreateBuilder(args);

// 1. Register Controllers & JSON Options
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    });

// 2. Register Health Checks
builder.Services.AddHealthChecks();

// 3. Register Swagger with JWT Bearer Authentication Support
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "NexaOps API",
        Version = "v0.1",
        Description = "NexaOps Enterprise Operations Backend - .NET Migration"
    });

    // Configure JWT in Swagger
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// 4. Global Exception Handling Middleware (Equivalent to NestJS HttpExceptionFilter)
app.UseMiddleware<GlobalExceptionMiddleware>();

// 5. Swagger UI
if (app.Environment.IsDevelopment() || true)
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "NexaOps API v0.1");
        c.RoutePrefix = string.Empty; // Swagger UI at root URL http://localhost:5000
    });
}

// 6. Routing & Endpoints
app.UseRouting();

// app.UseAuthentication();
// app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

// Export Program for Integration Testing fixture
public partial class Program { }
