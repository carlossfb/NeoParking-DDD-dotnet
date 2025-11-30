using main;
using Neoparking.Endpoints;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


builder.Services.AddAccessModule(builder.Configuration.GetConnectionString("Access") ?? "DefaultConnectionString");

var app = builder.Build();

// Inicializa o banco de dados
AccessModule.InitializeDatabase(app.Services);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Endpoints da WebAPI
app.MapClientEndpoints();

app.Run();
