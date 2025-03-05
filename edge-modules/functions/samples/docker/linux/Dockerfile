FROM mcr.microsoft.com/azure-functions/dotnet:4

ARG EXE_DIR=.

ENV AzureWebJobsScriptRoot=/app

WORKDIR /app

COPY $EXE_DIR/ ./
