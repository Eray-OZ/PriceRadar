# Build the Web application and its referenced projects.
FROM mcr.microsoft.com/dotnet/sdk:10.0-noble AS build

WORKDIR /src

COPY ["PriceRadar.Web/PriceRadar.Web.csproj", "PriceRadar.Web/"]
COPY ["PriceRadar.Data/PriceRadar.Data.csproj", "PriceRadar.Data/"]

RUN dotnet restore "PriceRadar.Web/PriceRadar.Web.csproj"

COPY . .
WORKDIR "/src/PriceRadar.Web"

RUN dotnet publish "PriceRadar.Web.csproj" \
    --configuration Release \
    --output /app/publish \
    /p:UseAppHost=false


# Run the lightweight Web application. Scraping runs in GitHub Actions.
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble AS final

WORKDIR /app

RUN useradd --create-home --shell /usr/sbin/nologin appuser

COPY --from=build /app/publish .

RUN chown --recursive appuser:appuser /app

USER appuser

ENV ASPNETCORE_HTTP_PORTS=10000

EXPOSE 10000

ENTRYPOINT ["dotnet", "PriceRadar.Web.dll"]
