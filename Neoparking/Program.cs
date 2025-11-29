using Microsoft.EntityFrameworkCore;
using src.application.dto;
using src.application.service;
using src.domain.ports;
using src.infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configurar InMemory Database + EF Core
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseInMemoryDatabase("NeoParkingDB"));

// Registrar dependências do módulo Access
builder.Services.AddScoped<IClientRepository, ClientRepositoryImpl>();
builder.Services.AddScoped<IClientService, ClientServiceImpl>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Endpoints
app.MapPost("/clients", async (ClientRequestDTO request, IClientService service) =>
{
    var result = await service.CreateClientAsync(request);
    return Results.Created($"/clients/{result.Id}", result);
});

app.MapGet("/clients/{id}", async (Guid id, IClientService service) =>
{
    var result = await service.GetClientByIdAsync(id);
    return result != null ? Results.Ok(result) : Results.NotFound();
});

app.Run();
