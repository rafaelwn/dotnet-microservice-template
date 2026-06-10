# Estágio 1: Build da aplicação usando o SDK do .NET 8
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-env
WORKDIR /app

# Copia os arquivos do projeto e restaura as dependências NuGet
COPY *.csproj ./
RUN dotnet restore

# Copia todo o restante dos arquivos e compila a aplicação no modo Release
COPY . ./
RUN dotnet publish -c Release -o out

# Estágio 2: Criação da imagem de execução leve usando apenas o Runtime do .NET 8
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build-env /app/out .

# Configura o ASP.NET Core para escutar na porta HTTP padrão do container
ENV ASPNETCORE_URLS=http://+:80

ENTRYPOINT ["dotnet", "MinhaApi.dll"]