ARG base_tag=2.1-runtime-nanoserver-1709
FROM microsoft/dotnet:${base_tag}

ARG EXE_DIR=.

WORKDIR /app

COPY $EXE_DIR/ ./

CMD ["dotnet", "TemperatureFilter.dll"]
