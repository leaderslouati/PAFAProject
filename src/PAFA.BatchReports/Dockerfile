# =============================================================================
# PAFA Batch — Data Ingestion & Report Generation Service
# Pipeline: SharePoint → Azure Blob Storage → Parse → Validate → Insert DB → Reports
#
# Usage:
#   docker build -t pafa-batch:local -f src/PAFA.BatchReports/Dockerfile .
#   docker run --rm pafa-batch:local --ingest
#   docker run --rm pafa-batch:local --ingest --year 2025 --month 7
#   docker run --rm pafa-batch:local --reports
#   docker run --rm pafa-batch:local --once
# =============================================================================

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["src/PAFA.BatchReports/PAFA.BatchReports.csproj",    "src/PAFA.BatchReports/"]
COPY ["src/PAFA.Reports/PAFA.Reports.csproj",               "src/PAFA.Reports/"]
COPY ["src/PAFA.Extraction/PAFA.Extraction.csproj",         "src/PAFA.Extraction/"]
COPY ["src/PAFA.Infrastructure/PAFA.Infrastructure.csproj", "src/PAFA.Infrastructure/"]
COPY ["src/PAFA.Domain/PAFA.Domain.csproj",                 "src/PAFA.Domain/"]

RUN dotnet restore "src/PAFA.BatchReports/PAFA.BatchReports.csproj"

COPY . .

WORKDIR /src/src/PAFA.BatchReports
RUN dotnet publish "PAFA.BatchReports.csproj" -c Release -o /app/publish

# ── Runtime ──────────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/runtime:9.0 AS runtime
WORKDIR /app

RUN apt-get update && \
    apt-get install -y --no-install-recommends libgdiplus libc6-dev && \
    rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

RUN mkdir -p /app/output/reports /app/output/reports/temp

ENV DOTNET_ENVIRONMENT=Production
ENV PAFA_BatchReports__OutputDirectory=/app/output/reports
ENV PAFA_BatchReports__TempDirectory=/app/output/reports/temp

RUN useradd -m -u 1000 pafa && chown -R pafa:pafa /app
USER pafa

ENTRYPOINT ["dotnet", "PAFA.BatchReports.dll"]
CMD ["--ingest"]