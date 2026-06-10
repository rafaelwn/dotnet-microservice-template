using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using MassTransit;
using MinhaApi.Domain.Entities;
using MinhaApi.Infrastructure.Data;

namespace MinhaApi.Features.Produtos;

public record CreateProdutoRequest(string Nome, decimal Preco);

// O contrato do evento que será enviado para a fila do RabbitMQ
public record ProdutoCriadoEvent(int Id, string Nome, decimal Preco);

public static class CreateProdutoEndpoint
{
    public static void MapCreateProduto(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/produtos", async (CreateProdutoRequest request, AppDbContext db, IPublishEndpoint publishEndpoint) =>
        {
            try
            {
                var produto = new Produto(request.Nome, request.Preco);
                db.Produtos.Add(produto);
                await db.SaveChangesAsync();

                // 🚀 PUBLICANDO NO RABBITMQ DE FORMA ASSÍNCRONA
                await publishEndpoint.Publish(new ProdutoCriadoEvent(produto.Id, produto.Nome, produto.Preco));

                return Results.Created($"/api/produtos/{produto.Id}", produto);
            }
            catch (ArgumentException ex)
            {
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
