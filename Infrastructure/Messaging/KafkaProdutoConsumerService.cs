using System.Text.Json;
using Confluent.Kafka;
using MinhaApi.Features.Produtos;

namespace MinhaApi.Infrastructure.Messaging;

// 📥 CONSUMIDOR KAFKA (BackgroundService rodando em loop dentro da própria API)
// Conceitos demonstrados:
// - Consumer Group: instâncias com o mesmo GroupId dividem as partições entre si
// - Offset: posição de leitura no log; o commit marca "até aqui eu já processei"
// - Diferente do RabbitMQ, a mensagem NÃO some do tópico após consumida
public sealed class KafkaProdutoConsumerService(IConfiguration config, ILogger<KafkaProdutoConsumerService> logger) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        // Consume() é bloqueante, então rodamos em uma Task dedicada
        // para não travar a inicialização da API
        Task.Run(() => ConsumirLoop(stoppingToken), stoppingToken);

    private void ConsumirLoop(CancellationToken stoppingToken)
    {
        var topico = config["Kafka:Topico"] ?? "produtos-criados";

        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = config["Kafka:BootstrapServers"] ?? "fila-kafka:19092",
            GroupId = config["Kafka:GrupoConsumidor"] ?? "minhaapi-consumidores",
            // Na primeira execução do grupo, começa do início do tópico
            AutoOffsetReset = AutoOffsetReset.Earliest,
            // Commit automático do offset a cada 5s (padrão didático; em produção avalie commit manual)
            EnableAutoCommit = true
        };

        using var consumer = new ConsumerBuilder<string, string>(consumerConfig)
            .SetErrorHandler((_, erro) => logger.LogWarning("⚠️ [Kafka] Erro no consumidor: {Motivo}", erro.Reason))
            .Build();

        consumer.Subscribe(topico);
        logger.LogInformation("👂 [Kafka] Consumidor inscrito no tópico '{Topico}' (grupo: {Grupo})", topico, consumerConfig.GroupId);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var resultado = consumer.Consume(stoppingToken);
                    if (resultado is null) continue;

                    var evento = JsonSerializer.Deserialize<ProdutoCriadoEvent>(resultado.Message.Value);

                    logger.LogInformation(
                        "📥 [Kafka] Evento consumido do tópico '{Topico}': ProdutoCriadoEvent {{ Id = {Id}, Nome = {Nome}, Preco = {Preco} }} | Partição: {Particao}, Offset: {Offset}",
                        topico, evento?.Id, evento?.Nome, evento?.Preco,
                        resultado.Partition.Value, resultado.Offset.Value);

                    // Aqui entraria a reação ao evento (igual ao consumidor do RabbitMQ)
                }
                catch (ConsumeException ex)
                {
                    logger.LogError("❌ [Kafka] Falha ao consumir: {Motivo}", ex.Error.Reason);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Encerramento normal da aplicação
        }
        finally
        {
            consumer.Close(); // Sai do consumer group de forma limpa
            logger.LogInformation("🛑 [Kafka] Consumidor finalizado");
        }
    }
}
