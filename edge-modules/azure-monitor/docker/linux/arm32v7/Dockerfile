# # based off of https://github.com/dotnet/dotnet-docker/blob/master/samples/dotnetapp/Dockerfile.debian-arm32
# FROM mcr.microsoft.com/dotnet/sdk:3.1-buster AS build-env
# WORKDIR /app

# # # copy csproj and restore as distinct layers
# COPY *.csproj ./
# RUN dotnet restore -r linux-arm

# # copy and publish app and libraries
# COPY . /tmp/pre-copy
# RUN rm -rf /tmp/pre-copy/bin /tmp/pre-copy/obj
# RUN mv /tmp/pre-copy/* ./

# RUN /app/write_vernum_print_script.sh

# RUN dotnet publish -c release -o out -r linux-arm --no-self-contained --no-restore

# # final stage/image
# FROM mcr.microsoft.com/dotnet/runtime:3.1-buster-slim-arm32v7
# WORKDIR /app
# COPY --from=build-env /app/out ./

# COPY --from=build-env /app/get_vernum.sh ./get_vernum.sh

# # TODO: put this back?
# # RUN useradd -ms /bin/bash moduleuser
# # USER moduleuser

# # add Application Insights instrumentation key into build
# ENV APPINSIGHTS_INSTRUMENTATIONKEY=NWQzN2MxYTQtYWZmOS00NmFhLTllMTgtZDA1ODZhOWMwYzg2

# ENTRYPOINT ["dotnet", "Microsoft.Azure.Devices.Edge.Azure.Monitor.dll"]

ARG base_tag=1.0.5.8-linux-arm32v7
FROM azureiotedge/azureiotedge-module-base:${base_tag}

ARG EXE_DIR=.

ENV MODULE_NAME "Microsoft.Azure.Devices.Edge.Azure.Monitor.dll"

WORKDIR /app

COPY $EXE_DIR/ ./

USER moduleuser

CMD echo "$(date --utc +"[%Y-%m-%d %H:%M:%S %:z]"): Starting Module" && \
    exec /usr/bin/dotnet Microsoft.Azure.Devices.Edge.Azure.Monitor.dll
