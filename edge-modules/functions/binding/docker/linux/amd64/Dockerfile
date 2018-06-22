FROM microsoft/azure-functions-dotnet-core2.0:2.0

WORKDIR /app

COPY $EXE_DIR/ ./bindings

CMD ["dotnet", "/azure-functions-host/Microsoft.Azure.WebJobs.Script.WebHost.dll"] 