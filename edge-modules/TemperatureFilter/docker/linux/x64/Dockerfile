FROM microsoft/dotnet:2.0.0-preview2-runtime-stretch

ARG EXE_DIR=.

WORKDIR /app

COPY $EXE_DIR/ ./

CMD ["/usr/bin/dotnet", "TemperatureFilter.dll"]
