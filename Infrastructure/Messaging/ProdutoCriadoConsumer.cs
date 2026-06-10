using MassTransit;
using MinhaApi.Features.Produtos;

namespace MinhaApi.Infrastructure.Messaging;

// 📥 CONSUMIDOR RABBITMQ (via MassTransit)
// O MassTransit cria automaticamente a fila "produto-criado" (definida no Program.cs),
// faz o bind com a exchange do evento e entrega cada mensagem para este consumidor.
public class ProdutoCriadoConsumer(ILogger<ProdutoCriadoConsumer> logger) : IConsumer<ProdutoCriadoEvent>
{
    public Task Consume(ConsumeContext<ProdutoCriadoEvent> context)
    {
        var evento = context.Message;

        logger.LogInformation(
            "📥 [RabbitMQ] Evento consumido da fila: ProdutoCriadoEvent {{ Id = {Id}, Nome = {Nome}, Preco = {Preco} }} | MessageId: {MessageId}",
            evento.Id, evento.Nome, evento.Preco, context.MessageId);

        // Aqui entraria a reação ao evento (enviar e-mail, atualizar cache, etc.)
        return Task.CompletedTask;
    }
}
