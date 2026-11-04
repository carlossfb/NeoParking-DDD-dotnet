using System.Reflection;
using MediatR;
using Microsoft.OpenApi.Models;
using NeoParking.Api.Endpoints;
using NeoParking.Api.Extensions;
using NeoParking.Api.Middleware;
using NeoParking.Shared.Kernel.Configuration;

var builder = WebApplication.CreateBuilder(args);

// O compose entrega o .env aos containers; no dotnet run local, quem lê é este passo.
if (builder.Environment.IsDevelopment())
{
    LocalEnvFile.Load();
    builder.Configuration.AddEnvironmentVariables();
}

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Informe apenas o token — sem o prefixo 'Bearer '. Ex: eyJhbGciOi..."
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddNeoParkingObservability();

// AppDomain.CurrentDomain.GetAssemblies() só retorna assemblies JÁ carregadas no processo —
// e, em .NET, uma assembly só é carregada na primeira vez que algo dela é efetivamente
// tocado. Como esta linha roda ANTES de AddNeoParkingModules (que é o primeiro ponto que
// toca Access/Billing/Management de verdade), dependendo do timing de carregamento do
// runtime os handlers de Billing e Management podem não estar disponíveis ainda — e
// IMediator.Publish() para uma notificação sem handlers não lança exceção nenhuma, só não
// faz nada. É exatamente esse tipo de falha silenciosa que fazia o TicketOpenedHandler do
// Billing nunca rodar. Listando as assemblies explicitamente, elas são sempre encontradas
// não importa a ordem de carregamento.
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssemblies(
        Assembly.GetExecutingAssembly(),
        typeof(NeoParking.Access.Infrastructure.AccessModule).Assembly,
        typeof(NeoParking.Billing.Infrastructure.BillingModule).Assembly,
        typeof(NeoParking.Management.Infrastructure.ManagementModule).Assembly));

builder.Services.AddNeoParkingModules(builder.Configuration);
builder.Services.AddNeoParkingAuthentication(builder.Configuration, builder.Environment);

var app = builder.Build();

app.UseMiddleware<RequestLoggingMiddleware>();
app.UseNeoParkingExceptionHandling();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Authentication/Authorization devem vir antes dos endpoints
app.UseAuthentication();
app.UseAuthorization();

app.MapClientEndpoints();
app.MapTicketEndpoints();
app.MapManagementEndpoints();
app.MapBillingEndpoints(); // <- registre aqui quando os endpoints do Billing existirem

var isDesignTime = Environment.GetEnvironmentVariable("EF_DESIGN_TIME") == "1";
if (!isDesignTime)
{
    app.Services.InitializeNeoParkingDatabases(builder.Configuration);
}

app.Run();

// Expõe o entry point para WebApplicationFactory nos testes E2E.
public partial class Program { }
