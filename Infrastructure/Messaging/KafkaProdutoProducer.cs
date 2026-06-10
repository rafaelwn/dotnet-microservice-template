using System.Text.Json;
using Confluent.Kafka;
using MinhaApi.Features.Produtos;

namespace MinhaApi.Infrastructure.Messaging;

// 📤 PRODUCER KAFKA (Confluent.Kafka, sem abstração — didático)
// Diferente do RabbitMQ (exchange/fila), no Kafka publicamos em um TÓPICO,
// que é um log particionado e durável: as mensagens ficam gravadas mesmo após consumidas.
public sealed class KafkaProdutoProducer : IDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly ILogger<KafkaProdutoProducer> _logger;
    private readonly string _topico;

    public KafkaProdutoProducer(IConfiguration config, ILogger<KafkaProdutoProducer> logger)
    {
        _logger = logger;
        _topico = config["Kafka:Topico"] ?? "produtos-criados";

        var producerConfig = new ProducerConfig
        {
            BootstrapServers = config["Kafka:BootstrapServers"] ?? "fila-kafka:19092",
            // Garante que o broker confirmou a gravação antes de considerarmos publicado
            Acks = Acks.All
        };

        _producer = new ProducerBuilder<string, string>(producerConfig).Build();
    }

    public async Task PublicarProdutoCriadoAsync(ProdutoCriadoEvent evento, CancellationToken ct = default)
    {
        var mensagem = new Message<string, string>
        {
            // A chave define a partição: eventos do mesmo produto sempre caem na mesma partição (ordem garantida)
            Key = evento.Id.ToString(),
            Value = JsonSerializer.Serialize(evento)
        };

        var resultado = await _producer.ProduceAsync(_topico, mensagem, ct);

        _logger.LogInformation(
            "📤 [Kafka] Evento publicado no tópico '{Topico}': ProdutoCriadoEvent {{ Id = {Id}, Nome = {Nome}, Preco = {Preco} }} | Partição: {Particao}, Offset: {Offset}",
            _topico, evento.Id, evento.Nome, evento.Preco, resultado.Partition.Value, resultado.Offset.Value);
    }

    public void Dispose() => _producer.Dispose();
}
