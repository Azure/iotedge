# syntax=docker/dockerfile:1.4

FROM mcr.microsoft.com/azure-functions/dotnet:3.0

ARG EXE_DIR=.

ENV AzureWebJobsScriptRoot=/app

WORKDIR /app

COPY $EXE_DIR/ ./