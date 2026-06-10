using Microsoft.EntityFrameworkCore;
using MassTransit;
using MinhaApi.Infrastructure.Data;
using MinhaApi.Infrastructure.Messaging;
using MinhaApi.Features.Produtos;
using MinhaApi.Features.Filas;

var builder = WebApplication.CreateBuilder(args);

// Infraestrutura: Banco de Dados
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=banco.db"));

// HttpClient usado pelas rotas de monitoramento de filas
builder.Services.AddHttpClient();

// 🚀 CONFIGURAÇÃO DO MASSTRANSIT COM RABBITMQ
builder.Services.AddMassTransit(x =>
{
    // 📥 Registra o consumidor que reage ao ProdutoCriadoEvent
    x.AddConsumer<ProdutoCriadoConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        // 'fila-rabbitmq' é o nome do container do RabbitMQ no docker-compose;
        // em desenvolvimento local (dotnet watch run) usa 'localhost' via appsettings.Development.json
        cfg.Host(builder.Configuration["RabbitMq:Host"] ?? "fila-rabbitmq", "/", h =>
        {
            h.Username(builder.Configuration["RabbitMq:User"] ?? "guest");
            h.Password(builder.Configuration["RabbitMq:Password"] ?? "guest");
        });

        // Cria a fila "produto-criado" e faz o bind com a exchange do evento
        cfg.ReceiveEndpoint("produto-criado", e =>
        {
            e.ConfigureConsumer<ProdutoCriadoConsumer>(context);
        });
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

app.MapCreateProduto();
app.MapGetFilasRabbitMq();

app.Logger.LogInformation("🚀 MinhaApi iniciada — rotas: POST/GET /api/produtos, GET /api/filas/rabbitmq");

app.Run();
