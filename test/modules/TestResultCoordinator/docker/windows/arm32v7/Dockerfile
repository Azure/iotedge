ARG base_tag=1.0.6-windows-arm32v7
FROM azureiotedge/azureiotedge-module-base:${base_tag}

ARG EXE_DIR=.

WORKDIR /app

# Expose HTTP port
EXPOSE 5001/tcp

COPY $EXE_DIR/ ./

CMD ["dotnet", "TestResultCoordinator.dll"]