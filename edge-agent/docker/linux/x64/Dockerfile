FROM microsoft/dotnet:2.0.0-preview2-runtime-stretch

ARG EXE_DIR=.
ENV DockerLoggingDriver=journald

WORKDIR /app

COPY $EXE_DIR/ ./

CMD ["/usr/bin/dotnet", "Microsoft.Azure.Devices.Edge.Agent.Service.dll"]
