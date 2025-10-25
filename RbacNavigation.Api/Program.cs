using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Net.Http.Headers;
using Microsoft.OpenApi.Models;
using RbacNavigation.Api.Authorization;
using RbacNavigation.Api.Data;
using RbacNavigation.Api.Services;
using Serilog;
using System.Text.Encodings.Web;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) =>
    {
        configuration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .WriteTo.Console();
    });

    builder.Services.AddControllers(options =>
    {
        options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
    }).AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    });

    builder.Services.Configure<RouteOptions>(options =>
    {
        options.LowercaseUrls = true;
        options.LowercaseQueryStrings = false;
    });

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(setup =>
    {
        setup.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "RBAC Navigation API",
            Version = "v1.1"
        });
        setup.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Name = HeaderNames.Authorization,
            Type = SecuritySchemeType.Http,
            Scheme = JwtBearerDefaults.AuthenticationScheme,
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "JWT Bearer token"
        });
        setup.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer",
                    }
                },
                Array.Empty<string>()
            }
        });
    });

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer();

    builder.Services.AddHttpContextAccessor();

    builder.Services.AddSingleton<HtmlEncoder>(_ => HtmlEncoder.Default);
    builder.Services.AddSingleton<NavigationContentSanitizer>();

    builder.Services.AddScoped<ICurrentUserContextAccessor, CurrentUserContextAccessor>();
    builder.Services.AddScoped<IAuthorizationHandler, PermissionsAuthorizationHandler>();
    builder.Services.AddScoped<IAuthorizationHandler, OrganizationScopeAuthorizationHandler>();

    builder.Services.AddAuthorization();

    builder.Services.AddSingleton<IDbConnectionFactory>(sp =>
    {
        var configuration = sp.GetRequiredService<IConfiguration>();
        var connectionString = configuration.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Connection string 'Default' must be configured.");
        }

        return new NpgsqlConnectionFactory(connectionString);
    });

    builder.Services.AddScoped<NavigationRepository>();
    builder.Services.AddScoped<NavigationComposer>();

    var app = builder.Build();

    app.UseSerilogRequestLogging();

    app.UseSwagger();
    app.UseSwaggerUI();

    app.UseHttpsRedirection();

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();

    Log.Information("RBAC Navigation API started");

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
