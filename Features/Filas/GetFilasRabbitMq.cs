using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace MinhaApi.Features.Filas;

// Resposta amigável com o estado de cada fila do RabbitMQ
public record FilaRabbitMqStatus(
    string Nome,
    long MensagensProntas,         // aguardando entrega a um consumidor
    long MensagensNaoConfirmadas,  // entregues mas ainda sem ACK
    long TotalMensagens,
    long TotalJaEntraram,          // histórico: quantas mensagens já entraram nesta fila
    long TotalJaEntregues,         // histórico: quantas já foram entregues a consumidores
    long Consumidores,
    string Estado);

public static class GetFilasRabbitMqEndpoint
{
    public static void MapGetFilasRabbitMq(this IEndpointRouteBuilder app)
    {
        // 📊 GET /api/filas/rabbitmq — consulta a Management API do RabbitMQ (porta 15672)
        // e responde quantas mensagens existem em cada fila e quantos consumidores estão conectados.
        app.MapGet("/api/filas/rabbitmq", async (IHttpClientFactory httpFactory, IConfiguration config, ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("MinhaApi.Filas.RabbitMq");

            var baseUrl = config["RabbitMq:ManagementUrl"] ?? "http://fila-rabbitmq:15672";
            var usuario = config["RabbitMq:User"] ?? "guest";
            var senha = config["RabbitMq:Password"] ?? "guest";

            var client = httpFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{usuario}:{senha}")));

            try
            {
                using var resposta = await client.GetAsync($"{baseUrl}/api/queues");
                resposta.EnsureSuccessStatusCode();

                using var json = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());

                var filas = json.RootElement.EnumerateArray()
                    .Select(fila => new FilaRabbitMqStatus(
                        Nome: fila.GetProperty("name").GetString() ?? "(sem nome)",
                        MensagensProntas: LerNumero(fila, "messages_ready"),
                        MensagensNaoConfirmadas: LerNumero(fila, "messages_unacknowledged"),
                        TotalMensagens: LerNumero(fila, "messages"),
                        TotalJaEntraram: LerEstatistica(fila, "publish"),
                        TotalJaEntregues: LerEstatistica(fila, "deliver_get"),
                        Consumidores: LerNumero(fila, "consumers"),
                        Estado: fila.TryGetProperty("state", out var estado) ? estado.GetString() ?? "?" : "?"))
                    .ToList();

                logger.LogInformation("📊 [RabbitMQ] Consulta de filas: {Quantidade} fila(s) encontrada(s)", filas.Count);

                return Results.Ok(new { Broker = "RabbitMQ", TotalFilas = filas.Count, Filas = filas });
            }
            catch (HttpRequestException ex)
            {
                logger.LogError(ex, "❌ [RabbitMQ] Falha ao consultar a Management API em {BaseUrl}", baseUrl);
                return Results.Problem(
                    title: "RabbitMQ indisponível",
                    detail: $"Não foi possível consultar a Management API em {baseUrl}: {ex.Message}",
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }
        });
    }

    // Campos numéricos podem vir ausentes na Management API logo após a criação da fila
    private static long LerNumero(JsonElement elemento, string propriedade) =>
        elemento.TryGetProperty(propriedade, out var valor) && valor.ValueKind == JsonValueKind.Number
            ? valor.GetInt64()
            : 0;

    // Contadores históricos ficam dentro de "message_stats" e só aparecem após a primeira mensagem
    private static long LerEstatistica(JsonElement fila, string propriedade) =>
        fila.TryGetProperty("message_stats", out var stats) ? LerNumero(stats, propriedade) : 0;
}
