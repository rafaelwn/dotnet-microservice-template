**Architecture**

📂 MinhaApi/
├── 📂 Domain/                └── 🧠 O Coração (Puro C#)
│   ├── 📂 Entities/             └── Entidades de negócio
│   └── 📂 Exceptions/           └── Exceções de domínio
│
├── 📂 Features/              └── 🔪 Casos de Uso (Vertical Slices)
│   └── 📂 Produtos/
│       ├── CreateProduto.cs     └── Rota, Validação, Handler de criação
│       └── GetProdutos.cs       └── Rota e Handler de listagem
│
├── 📂 Infrastructure/        └── 🔌 Adaptadores (Mundo Externo)
│   ├── 📂 Data/                 └── EF Core Context, Migrations
│   └── 📂 Messaging/            └── Consumidores RabbitMQ / Kafka
│
└── 📄 Program.cs             └── ⛓️ O Orquestrador (Injeção de dependência)


# 🚀 .NET 8 Microservice — Minimal API, DDD & RabbitMQ

Este é um projeto de microsserviço moderno desenvolvido em **.NET 8 (SDK 8.0.127)**, estruturado para rodar nativamente em ambientes **Linux** utilizando **Docker**. O objetivo principal é demonstrar as melhores práticas de mercado na transição de ecossistemas como Node.js para o ambiente .NET moderno de alta performance.

---

## 🛠️ Tecnologias e Padrões Utilizados

- **.NET 8 (C# 12):** Utilização de recursos modernos como *Top-Level Statements*, *Records* e *Primary Constructors*.
- **Minimal APIs:** Criação de endpoints leves, performáticos e sem o boilerplate clássico de Controllers (sintaxe limpa similar ao Express/Fastify do Node.js).
- **Entity Framework Core 8 (EF Core):** Mapeador Objeto-Relacional utilizado com o provedor **SQLite** para persistência local leve.
- **Domain-Driven Design (DDD Tático):** Isolamento de regras de negócio na entidade de Domínio com construtores auto-validados.
- **Vertical Slice Architecture:** Organização modular do código por funcionalidade (caso de uso) e não por camadas técnicas horizontais.
- **MassTransit & RabbitMQ:** Abstração e mensageria assíncrona baseada em eventos (Publish/Subscribe).
- **Docker & Docker Compose:** Containerização multiplataforma com *Multi-Stage Build* para otimização do tamanho da imagem final.

---

## 📁 Estrutura de Pastas (Arquitetura Modular)

```text
📂 MinhaApi/
├── 📂 Domain/                # 🧠 Regras de negócio puras (Sem dependências externas)
│   └── 📂 Entities/             └─ Produto.cs (Entidade de Domínio estruturada com DDD)
│
├── 📂 Features/              # 🔪 Casos de Uso (Vertical Slices / Fatias Verticais)
│   └── 📂 Produtos/             └─ CreateProduto.cs (DTO, Rota, Handler e Evento)
│
├── 📂 Infrastructure/        # 🔌 Adaptadores e Conexões Externas
│   └── 📂 Data/                 └─ AppDbContext.cs (Contexto de dados do EF Core)
│
├── 📄 Dockerfile             # 🐳 Multi-stage Build otimizado para o .NET 8
├── 📄 docker-compose.yml     # ⛓️ Orquestrador local (Microsserviço + Container RabbitMQ)
└── 📄 Program.cs             # 🎛️ Bootstrapper e Injeção de Dependências
```

---

## 🚀 Como Executar o Projeto

### Pré-requisitos
- **Docker** e **Docker Compose** instalados na máquina (Linux/WSL2/Mac).

### Passos para Inicialização

1. **Clone o repositório e acesse a pasta:**
   ```bash
   cd MinhaApi
   ```

2. **Suba todo o ambiente local (Microsserviço + RabbitMQ):**
   O Docker Compose irá baixar as imagens do RabbitMQ, compilar o código fonte C# através do SDK do .NET 8 e criar a infraestrutura interna de comunicação:
   ```bash
   docker compose up --build
   ```

---

## 🧪 Testando o Fluxo de Ponta a Ponta

### 1. Painel de Controle do RabbitMQ
Abra o navegador e acesse a interface de gerenciamento visual da fila:
- **URL:** `http://localhost:15672`
- **Usuário:** `guest`
- **Senha:** `guest`
*Verifique na aba **Queues** que o MassTransit criou automaticamente os contratos de mensageria baseados em seus Records do C#.*

### 2. Criar um Produto (Gatilho do Evento)
Abra o terminal ou seu cliente HTTP de preferência (Postman/Insomnia) e envie uma requisição `POST`:

```bash
curl -X POST http://localhost:5000/api/produtos \
     -H "Content-Type: application/json" \
     -d '{"nome": "Notebook Linux Pro", "preco": 4500.00}'
```

**O que acontece por trás dos panos:**
1. A **Minimal API** recebe o JSON.
2. A entidade de **Domínio** valida se as regras estão corretas (Preço não negativo, nome preenchido).
3. O **EF Core 8** persiste o registro no arquivo de banco SQLite (`banco.db`) interno do container.
4. O **MassTransit** intercepta e publica o evento `ProdutoCriadoEvent` de forma assíncrona diretamente para a exchange/fila correspondente no **RabbitMQ**.

---

## 💻 Dica para Desenvolvimento Local (Hot Reload)

Se preferir codar na máquina física sem a necessidade de gerar um `build` do Docker a cada modificação, você pode aproveitar o ciclo de feedback rápido do .NET utilizando:

```bash
dotnet watch run
```
Qualquer alteração salva nos arquivos C# disparará um mecanismo de **Hot Reload**, atualizando a API local em milissegundos, de forma idêntica à experiência de desenvolvimento encontrada no ecossistema Node.js.

