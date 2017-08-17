FROM edgebuilds.azurecr.io/azure-functions-runtime-armv7hf:2.0.0-beta1-aaebce7

WORKDIR /app

COPY $EXE_DIR/ ./bindings

COPY ./linux/downloadAndRun.sh /app

ENV AzureWebJobs_ExtensionsPath /app

CMD ["./downloadAndRun.sh"]