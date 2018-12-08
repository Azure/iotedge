ARG base_tag=1.0.0-windows-arm32v7
ARG registry
FROM ${registry}/azureiotedge/azureiotedge-module-base:${base_tag}

ARG EXE_DIR=.

WORKDIR /app

COPY $EXE_DIR/ ./

USER moduleuser
CMD ["dotnet", "MessagesAnalyzer.dll"]