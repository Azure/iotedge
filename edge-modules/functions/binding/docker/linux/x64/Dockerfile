FROM edgebuilds.azurecr.io/azure-functions-runtime-x64:aaebce7

WORKDIR /app

COPY $EXE_DIR/ ./bindings

COPY ./linux/downloadAndRun.sh /app

ENV AzureWebJobs_ExtensionsPath /app

CMD ["./downloadAndRun.sh"]