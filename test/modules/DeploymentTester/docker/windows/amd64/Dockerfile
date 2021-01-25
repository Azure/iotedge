ARG base_tag=3.1.11-nanoserver-1809
FROM mcr.microsoft.com/dotnet/core/runtime:${base_tag}

ENV MODULE_NAME "DeploymentTester.dll"

ARG EXE_DIR=.

WORKDIR /app

COPY $EXE_DIR/ ./

CMD ["dotnet", "DeploymentTester.dll"]
