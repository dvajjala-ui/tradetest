# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
COPY Directory.Build.props ./
COPY src/TradeTest.Domain/ src/TradeTest.Domain/
COPY src/TradeTest.Application/ src/TradeTest.Application/
COPY src/TradeTest.Infrastructure/ src/TradeTest.Infrastructure/
COPY src/TradeTest.Api/ src/TradeTest.Api/
RUN --mount=type=secret,id=proxy_ca \
    if [ -f /run/secrets/proxy_ca ]; then export SSL_CERT_FILE=/run/secrets/proxy_ca; fi; \
    dotnet publish src/TradeTest.Api/TradeTest.Api.csproj -c Release -o /publish --nologo

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /publish ./
COPY fixtures/ /app/research/fixtures/
COPY benchmarks/results/ /app/research/benchmarks/results/
ENV TRADETEST_ROOT=/app/research
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "TradeTest.Api.dll"]
