ARG base_tag=3.1-nanoserver-1809
FROM mcr.microsoft.com/dotnet/aspnet:${base_tag}

ARG EXE_DIR=.

WORKDIR /app

COPY $EXE_DIR/ ./

CMD ["dotnet", "TestMetricsCollector.dll"]
