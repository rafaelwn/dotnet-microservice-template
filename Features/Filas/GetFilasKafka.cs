using Confluent.Kafka;
using Confluent.Kafka.Admin;

namespace MinhaApi.Features.Filas;

public record ParticaoKafkaStatus(
    int Particao,
    long PrimeiroOffset,
    long UltimoOffset,
    long TotalEventos,           // quantos eventos existem gravados na partição
    long OffsetCommitado,        // até onde o consumer group já confirmou leitura (-1 = nunca)
    long EventosNaoConsumidos);  // lag: quantos eventos ainda não foram processados pelo grupo

public record TopicoKafkaStatus(
    string Nome,
    int Particoes,
    long TotalEventos,
    long EventosNaoConsumidos,
    List<ParticaoKafkaStatus> DetalhePorParticao);

public static class GetFilasKafkaEndpoint
{
    public static void MapGetFilasKafka(this IEndpointRouteBuilder app)
    {
        // 📊 GET /api/filas/kafka — consulta os metadados do broker e responde:
        // quantos eventos existem em cada tópico (via offsets do log) e quantos
        // ainda não foram consumidos pelo consumer group (lag).
        app.MapGet("/api/filas/kafka", async (IConfiguration config, ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("MinhaApi.Filas.Kafka");

            var bootstrapServers = config["Kafka:BootstrapServers"] ?? "fila-kafka:19092";
            var grupoConsumidor = config["Kafka:GrupoConsumidor"] ?? "minhaapi-consumidores";

            using var admin = new AdminClientBuilder(new AdminClientConfig { BootstrapServers = bootstrapServers }).Build();

            try
            {
                var metadata = admin.GetMetadata(TimeSpan.FromSeconds(5));

                // Ignora tópicos internos do Kafka (ex.: __consumer_offsets)
                var topicos = metadata.Topics.Where(t => !t.Topic.StartsWith("__")).ToList();

                var particoes = topicos
                    .SelectMany(t => t.Partitions.Select(p => new TopicPartition(t.Topic, p.PartitionId)))
                    .ToList();

                if (particoes.Count == 0)
                {
                    return Results.Ok(new { Broker = "Kafka", TotalTopicos = 0, Topicos = new List<TopicoKafkaStatus>(), Aviso = "Nenhum tópico criado ainda — publique um produto para criar o tópico." });
                }

                // Offsets inicial e final de cada partição (o "tamanho" do log)
                var inicios = await admin.ListOffsetsAsync(
                    particoes.Select(tp => new TopicPartitionOffsetSpec { TopicPartition = tp, OffsetSpec = OffsetSpec.Earliest() }),
                    new ListOffsetsOptions());
                var fins = await admin.ListOffsetsAsync(
                    particoes.Select(tp => new TopicPartitionOffsetSpec { TopicPartition = tp, OffsetSpec = OffsetSpec.Latest() }),
                    new ListOffsetsOptions());

                // Offsets já confirmados (commitados) pelo consumer group
                var commitados = new Dictionary<TopicPartition, long>();
                try
                {
                    var resultadoGrupo = await admin.ListConsumerGroupOffsetsAsync(
                        new[] { new ConsumerGroupTopicPartitions(grupoConsumidor, particoes) });

                    foreach (var tpo in resultadoGrupo.SelectMany(r => r.Partitions))
                        commitados[tpo.TopicPartition] = tpo.Offset.Value;
                }
                catch (KafkaException)
                {
                    // Grupo ainda não existe (consumidor nunca commitou) — lag = total de eventos
                }

                var primeiroPorParticao = inicios.ResultInfos.ToDictionary(r => r.TopicPartitionOffsetError.TopicPartition, r => r.TopicPartitionOffsetError.Offset.Value);
                var ultimoPorParticao = fins.ResultInfos.ToDictionary(r => r.TopicPartitionOffsetError.TopicPartition, r => r.TopicPartitionOffsetError.Offset.Value);

                var status = topicos.Select(t =>
                {
                    var detalhes = t.Partitions.Select(p =>
                    {
                        var tp = new TopicPartition(t.Topic, p.PartitionId);
                        var primeiro = primeiroPorParticao.GetValueOrDefault(tp, 0);
                        var ultimo = ultimoPorParticao.GetValueOrDefault(tp, 0);
                        var commitado = commitados.GetValueOrDefault(tp, Offset.Unset.Value);

                        // Sem commit ainda: nada foi confirmado, então o lag é o log inteiro
                        var lag = commitado < 0 ? ultimo - primeiro : ultimo - commitado;

                        return new ParticaoKafkaStatus(
                            Particao: p.PartitionId,
                            PrimeiroOffset: primeiro,
                            UltimoOffset: ultimo,
                            TotalEventos: ultimo - primeiro,
                            OffsetCommitado: commitado < 0 ? -1 : commitado,
                            EventosNaoConsumidos: long.Max(lag, 0));
                    }).ToList();

                    return new TopicoKafkaStatus(
                        Nome: t.Topic,
                        Particoes: detalhes.Count,
                        TotalEventos: detalhes.Sum(d => d.TotalEventos),
                        EventosNaoConsumidos: detalhes.Sum(d => d.EventosNaoConsumidos),
                        DetalhePorParticao: detalhes);
                }).ToList();

                logger.LogInformation("📊 [Kafka] Consulta de tópicos: {Quantidade} tópico(s) encontrado(s)", status.Count);

                return Results.Ok(new { Broker = "Kafka", ConsumerGroup = grupoConsumidor, TotalTopicos = status.Count, Topicos = status });
            }
            catch (KafkaException ex)
            {
                logger.LogError(ex, "❌ [Kafka] Falha ao consultar o broker em {BootstrapServers}", bootstrapServers);
                return Results.Problem(
                    title: "Kafka indisponível",
                    detail: $"Não foi possível consultar o broker em {bootstrapServers}: {ex.Message}",
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }
        });
    }
}
