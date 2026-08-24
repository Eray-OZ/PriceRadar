# Build the Web application and its referenced projects.
FROM mcr.microsoft.com/dotnet/sdk:10.0-noble AS build

WORKDIR /src

COPY ["HBTracker.Web/HBTracker.Web.csproj", "HBTracker.Web/"]
COPY ["HBTracker.Data/HBTracker.Data.csproj", "HBTracker.Data/"]
COPY ["HBTracker.Scraping/HBTracker.Scraping.csproj", "HBTracker.Scraping/"]

RUN dotnet restore "HBTracker.Web/HBTracker.Web.csproj"

COPY . .
WORKDIR "/src/HBTracker.Web"

RUN dotnet publish "HBTracker.Web.csproj" \
    --configuration Release \
    --output /app/publish \
    /p:UseAppHost=false


# Run the published Web application with Google Chrome available for Playwright.
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble AS final

WORKDIR /app

RUN apt-get update \
    && apt-get install --yes --no-install-recommends \
        ca-certificates \
        curl \
        fonts-liberation \
        gnupg \
        wget \
    && mkdir --parents --mode=0755 /etc/apt/keyrings \
    && curl --fail --silent --show-error https://dl.google.com/linux/linux_signing_key.pub \
        | gpg --dearmor --output /etc/apt/keyrings/google-chrome.gpg \
    && echo "deb [arch=amd64 signed-by=/etc/apt/keyrings/google-chrome.gpg] http://dl.google.com/linux/chrome/deb/ stable main" \
        > /etc/apt/sources.list.d/google-chrome.list \
    && apt-get update \
    && apt-get install --yes --no-install-recommends google-chrome-stable \
    && rm --recursive --force /var/lib/apt/lists/*

RUN useradd --create-home --shell /usr/sbin/nologin appuser

COPY --from=build /app/publish .

RUN chown --recursive appuser:appuser /app

USER appuser

ENV ASPNETCORE_HTTP_PORTS=10000

EXPOSE 10000

ENTRYPOINT ["dotnet", "HBTracker.Web.dll"]
