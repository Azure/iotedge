ARG base_tag=1.0.3-windows-arm32v7
ARG base_registry
FROM ${base_registry}/azureiotedge/azureiotedge-module-base:${base_tag}

ARG EXE_DIR=.

WORKDIR /app

COPY $EXE_DIR/ ./

CMD ["dotnet", "DirectMethodSender.dll"]