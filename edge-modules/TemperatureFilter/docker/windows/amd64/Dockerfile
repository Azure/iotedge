FROM microsoft/dotnet:2.0.6-runtime-nanoserver-1709

ARG EXE_DIR=.

WORKDIR /app

COPY $EXE_DIR/ ./

CMD ["dotnet", "TemperatureFilter.dll"]
