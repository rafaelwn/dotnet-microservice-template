using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MassTransit;
using MinhaApi.Infrastructure.Data;
using MinhaApi.Features.Produtos;

var builder = WebApplication.CreateBuilder(args);

// Infraestrutura: Banco de Dados
builder.Services.AddDbContext<AppDbContext>(options => 
    options.UseSqlite("Data Source=banco.db"));

// 🚀 CONFIGURAÇÃO DO MASSTRANSIT COM RABBITMQ
builder.Services.AddMassTransit(x =>
{
    x.UsingRabbitMq((context, cfg) =>
    {
        // 'fila-rabbitmq' será o nome do container do RabbitMQ que definiremos no docker-compose
        cfg.Host("fila-rabbitmq", "/", h =>
        {
            h.Username("guest");
            h.Password("guest");
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

app.Run();
