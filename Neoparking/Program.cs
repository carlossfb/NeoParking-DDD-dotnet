using main;
using Neoparking.Endpoints;
using static main.DatabaseProvider;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


builder.Services.AddAccessModule(builder.Configuration);

var app = builder.Build();

// Inicializa o banco de dados
var databaseProvider = AccessModule.GetDatabaseProvider(builder.Configuration);
AccessModule.InitializeDatabase(app.Services, databaseProvider);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Endpoints da WebAPI
app.MapClientEndpoints();

app.Run();
