using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using MassTransit;
using MinhaApi.Domain.Entities;
using MinhaApi.Infrastructure.Data;
using MinhaApi.Infrastructure.Messaging;

namespace MinhaApi.Features.Produtos;

public record CreateProdutoRequest(string Nome, decimal Preco);

// O contrato do evento que será enviado para a fila do RabbitMQ
public record ProdutoCriadoEvent(int Id, string Nome, decimal Preco);

public static class CreateProdutoEndpoint
{
    public static void MapCreateProduto(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/produtos", async (CreateProdutoRequest request, AppDbContext db, IPublishEndpoint publishEndpoint, KafkaProdutoProducer kafkaProducer, ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("MinhaApi.Produtos");

            try
            {
                var produto = new Produto(request.Nome, request.Preco);
                db.Produtos.Add(produto);
                await db.SaveChangesAsync();

                // 🚀 PUBLICANDO NO RABBITMQ DE FORMA ASSÍNCRONA
                var evento = new ProdutoCriadoEvent(produto.Id, produto.Nome, produto.Preco);
                await publishEndpoint.Publish(evento);

                logger.LogInformation(
                    "📤 [RabbitMQ] Evento publicado: ProdutoCriadoEvent {{ Id = {Id}, Nome = {Nome}, Preco = {Preco} }}",
                    evento.Id, evento.Nome, evento.Preco);

                // 🚀 PUBLICANDO O MESMO EVENTO NO TÓPICO DO KAFKA
                await kafkaProducer.PublicarProdutoCriadoAsync(evento);

                return Results.Created($"/api/produtos/{produto.Id}", produto);
            }
            catch (ArgumentException ex)
            {
                logger.LogWarning("⚠️ Produto rejeitado pela validação de domínio: {Erro}", ex.Message);
                return Results.BadRequest(new { Erro = ex.Message });
            }
        });

        app.MapGet("/api/produtos", async (AppDbContext db) =>
        {
            var produtos = await db.Produtos.ToListAsync();
            return Results.Ok(produtos);
        });
    }
}
