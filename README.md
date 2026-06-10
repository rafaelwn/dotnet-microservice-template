# 🚀 .NET 8 Microservice — Minimal API, DDD, RabbitMQ & Kafka

Este é um projeto de microsserviço moderno desenvolvido em **.NET 8 (SDK 8.0.127)**, estruturado para rodar nativamente em ambientes **Linux** utilizando **Docker**. O objetivo principal é **demonstrar o uso de mensageria (filas e tópicos) com exemplos simples, funcionais e fáceis de acompanhar** — ideal para estudo da transição de ecossistemas como Node.js para o .NET moderno.

O mesmo evento de negócio (`ProdutoCriadoEvent`) é publicado em **dois brokers ao mesmo tempo**, permitindo comparar lado a lado:

| | 🐰 RabbitMQ | 📨 Apache Kafka |
|---|---|---|
| Modelo | Fila (a mensagem some após o ACK) | Tópico/log (a mensagem fica gravada) |
| Biblioteca | MassTransit (abstração) | Confluent.Kafka (cliente direto) |
| Consumidor | `IConsumer<T>` do MassTransit | `BackgroundService` com loop de `Consume` |
| Conceitos | Exchange, fila, ACK | Partição, offset, consumer group, lag |

---

## 🛠️ Tecnologias e Padrões Utilizados

- **.NET 8 (C# 12):** *Top-Level Statements*, *Records* e *Primary Constructors*.
- **Minimal APIs:** Endpoints leves e sem boilerplate (sintaxe similar ao Express/Fastify do Node.js).
- **Entity Framework Core 8:** ORM com provedor **SQLite** para persistência local leve.
- **DDD Tático + Vertical Slice Architecture:** Regras de negócio na entidade de domínio; código organizado por funcionalidade.
- **MassTransit & RabbitMQ:** Mensageria assíncrona baseada em eventos (Publish/Subscribe).
- **Confluent.Kafka & Apache Kafka (KRaft):** Producer e consumer "na unha" para expor os conceitos reais do Kafka.
- **Docker & Docker Compose:** 4 containers orquestrados com *healthchecks* e *Multi-Stage Build*.

---

## 📁 Estrutura de Pastas (Arquitetura Modular)

```text
📂 MinhaApi/
├── 📂 Domain/                       # 🧠 Regras de negócio puras (sem dependências externas)
│   └── 📂 Entities/                    └─ Produto.cs (Entidade de domínio com DDD)
│
├── 📂 Features/                     # 🔪 Casos de Uso (Vertical Slices)
│   ├── 📂 Produtos/                    └─ CreateProduto.cs (DTO, rota, handler e evento)
│   └── 📂 Filas/                       └─ GetFilasRabbitMq.cs / GetFilasKafka.cs (monitoramento)
│
├── 📂 Infrastructure/               # 🔌 Adaptadores e conexões externas
│   ├── 📂 Data/                        └─ AppDbContext.cs (EF Core)
│   └── 📂 Messaging/                   └─ ProdutoCriadoConsumer.cs (RabbitMQ)
│                                       └─ KafkaProdutoProducer.cs / KafkaProdutoConsumerService.cs
│
├── 📄 Dockerfile                    # 🐳 Multi-stage build do .NET 8
├── 📄 docker-compose.yaml           # ⛓️ API + RabbitMQ + Kafka + Kafka UI
└── 📄 Program.cs                    # 🎛️ Bootstrapper e injeção de dependências
```

---

## 🚀 Como Executar o Projeto

### Pré-requisitos
- **Docker** e **Docker Compose** instalados (Linux/WSL2/Mac).

### Suba todo o ambiente

```bash
docker compose up --build
```

Containers criados:

| Container | Função | Acesso |
|---|---|---|
| `microsservico-net` | A API .NET 8 | http://localhost:5000 |
| `fila-rabbitmq` | Broker RabbitMQ + painel | http://localhost:15672 (guest/guest) |
| `fila-kafka` | Broker Kafka (KRaft, sem Zookeeper) | localhost:9092 |
| `kafka-ui` | Painel visual do Kafka | http://localhost:8080 |

---

## 🗺️ Rotas da API

| Método | Rota | Descrição |
|---|---|---|
| `POST` | `/api/produtos` | Cria produto, grava no SQLite e publica o evento no RabbitMQ **e** no Kafka |
| `GET` | `/api/produtos` | Lista os produtos persistidos |
| `GET` | `/api/filas/rabbitmq` | Estado das filas: mensagens prontas, não confirmadas, histórico de entradas/entregas e consumidores |
| `GET` | `/api/filas/kafka` | Estado dos tópicos: total de eventos por partição, offset commitado e lag do consumer group |

---

## 🧪 Testando o Fluxo de Ponta a Ponta

### 1. Crie um produto (dispara os eventos)

```bash
curl -X POST http://localhost:5000/api/produtos \
     -H "Content-Type: application/json" \
     -d '{"nome": "Notebook Linux Pro", "preco": 4500.00}'
```

**O que acontece por trás dos panos:**
1. A **Minimal API** recebe o JSON e a entidade de **domínio** valida as regras (preço não negativo, nome obrigatório).
2. O **EF Core** persiste o registro no SQLite (`banco.db`).
3. O **MassTransit** publica o `ProdutoCriadoEvent` no **RabbitMQ** → o `ProdutoCriadoConsumer` consome da fila `produto-criado`.
4. O **KafkaProdutoProducer** publica o mesmo evento no tópico `produtos-criados` → o `KafkaProdutoConsumerService` consome em background.

### 2. Acompanhe pelos logs

```bash
docker logs -f microsservico-net
```

Legenda dos logs (fáceis de filtrar):

```text
📤 [RabbitMQ] Evento publicado: ProdutoCriadoEvent { Id = 1, ... }
📥 [RabbitMQ] Evento consumido da fila: ProdutoCriadoEvent { Id = 1, ... }
📤 [Kafka]    Evento publicado no tópico 'produtos-criados': ... | Partição: 0, Offset: 0
📥 [Kafka]    Evento consumido do tópico 'produtos-criados': ... | Partição: 0, Offset: 0
👂 [Kafka]    Consumidor inscrito no tópico (grupo: minhaapi-consumidores)
📊            Consulta às rotas de monitoramento
```

### 3. Consulte as rotas de monitoramento

```bash
curl http://localhost:5000/api/filas/rabbitmq
```
```json
{
  "broker": "RabbitMQ",
  "totalFilas": 1,
  "filas": [{
    "nome": "produto-criado",
    "mensagensProntas": 0,
    "mensagensNaoConfirmadas": 0,
    "totalMensagens": 0,
    "totalJaEntraram": 3,
    "totalJaEntregues": 3,
    "consumidores": 1,
    "estado": "running"
  }]
}
```

```bash
curl http://localhost:5000/api/filas/kafka
```
```json
{
  "broker": "Kafka",
  "consumerGroup": "minhaapi-consumidores",
  "totalTopicos": 1,
  "topicos": [{
    "nome": "produtos-criados",
    "particoes": 1,
    "totalEventos": 2,
    "eventosNaoConsumidos": 0,
    "detalhePorParticao": [{
      "particao": 0,
      "primeiroOffset": 0,
      "ultimoOffset": 2,
      "totalEventos": 2,
      "offsetCommitado": 2,
      "eventosNaoConsumidos": 0
    }]
  }]
}
```

💡 **Experimento didático:** consulte `/api/filas/kafka` imediatamente após criar um produto — você verá `eventosNaoConsumidos > 0` até o consumer group commitar o offset (auto-commit a cada 5s). No RabbitMQ, repare que `totalMensagens` quase sempre é `0` (a fila esvazia após o ACK), mas `totalJaEntraram` guarda o histórico.

### 4. Painéis visuais

- **RabbitMQ:** http://localhost:15672 (guest/guest) → aba **Queues** mostra a fila `produto-criado`.
- **Kafka UI:** http://localhost:8080 → tópico `produtos-criados`, mensagens, partições e consumer groups.

---

## 💻 Dica para Desenvolvimento Local (Hot Reload)

Com os brokers rodando via Docker (`docker compose up fila-rabbitmq fila-kafka kafka-ui`), você pode rodar a API na máquina física com ciclo de feedback rápido:

```bash
dotnet watch run
```

O `appsettings.Development.json` já aponta para `localhost` (RabbitMQ em `localhost:5672` e Kafka em `localhost:9092`). Qualquer alteração salva dispara o **Hot Reload**, de forma idêntica à experiência do ecossistema Node.js.
