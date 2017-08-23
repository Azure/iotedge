FROM edgebuilds.azurecr.io/azure-functions-runtime-x64:2.0.0-beta1-0c7272b

WORKDIR /app

COPY $EXE_DIR/ ./bindings

COPY ./linux/downloadAndRun.sh /app

ENV AzureWebJobs_ExtensionsPath /app

CMD ["./downloadAndRun.sh"]