using System.Text;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using NeoParking.Access.Infrastructure;
using NeoParking.Api;
using NeoParking.Api.Endpoints;
using NeoParking.Api.Middleware;
using NeoParking.Management.Application;
using NeoParking.Management.Infrastructure;
using NeoParking.Shared.Kernel.Events;
using NeoParking.Shared.Kernel.Exceptions;
using NeoParking.Shared.Kernel.Observability;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// OpenTelemetry — ativa tracing nativo do ASP.NET e HttpClient
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(serviceName: "neoparking-api", serviceVersion: "1.0.0"))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation());

// HttpClient com propagação automática de traceparent
builder.Services.AddHttpClient();

// MediatR
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssemblies(AppDomain.CurrentDomain.GetAssemblies()));

// Observabilidade
builder.Services.AddScoped<ICorrelationIdProvider, HttpCorrelationIdProvider>();
builder.Services.AddScoped<IEventDispatcher, MediatREventDispatcher>();

builder.Services.AddAccessModule(builder.Configuration);
builder.Services.AddManagementModule(builder.Configuration);

builder.Services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();

// JWT Authentication
var jwtSecret = builder.Configuration["Jwt:Secret"]
    ?? throw new InvalidOperationException("Jwt:Secret not configured.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidIssuer              = builder.Configuration["Jwt:Issuer"] ?? "neoparking",
            ValidateAudience         = true,
            ValidAudience            = builder.Configuration["Jwt:Audience"] ?? "neoparking",
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

app.UseMiddleware<RequestLoggingMiddleware>();

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var exception = context.Features
            .Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = exception switch
        {
            NotFoundException => StatusCodes.Status404NotFound,
            DomainException   => StatusCodes.Status422UnprocessableEntity,
            _                 => StatusCodes.Status500InternalServerError
        };

        var message = exception switch
        {
            NotFoundException or DomainException => exception.Message,
            _ => "An unexpected error occurred"
        };

        await context.Response.WriteAsJsonAsync(new
        {
            status = context.Response.StatusCode,
            error  = message
        });
    });
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Authentication/Authorization devem vir antes dos endpoints
app.UseAuthentication();
app.UseAuthorization();

app.MapClientEndpoints();
app.MapManagementEndpoints();

var isDesignTime = Environment.GetEnvironmentVariable("EF_DESIGN_TIME") == "1";
if (!isDesignTime)
{
    AccessModule.InitializeDatabase(app.Services, builder.Configuration);
    ManagementModule.InitializeDatabase(app.Services);
}

app.Run();
