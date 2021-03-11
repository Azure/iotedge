# FROM mcr.microsoft.com/dotnet/core/sdk:3.1-buster AS build-env
# WORKDIR /app

# COPY *.csproj ./
# RUN dotnet restore

# # COPY . ./
# COPY . /tmp/pre-copy
# RUN rm -rf /tmp/pre-copy/bin /tmp/pre-copy/obj
# RUN mv /tmp/pre-copy/* ./

# RUN /app/write_vernum_print_script.sh

# RUN dotnet publish -c Release -o out

# FROM mcr.microsoft.com/dotnet/core/runtime:3.1-buster-slim
# WORKDIR /app
# COPY --from=build-env /app/out ./

# COPY --from=build-env /app/get_vernum.sh ./get_vernum.sh

# RUN useradd -ms /bin/bash moduleuser
# USER moduleuser

# # add Application Insights instrumentation key into build
# ENV APPINSIGHTS_INSTRUMENTATIONKEY=NWQzN2MxYTQtYWZmOS00NmFhLTllMTgtZDA1ODZhOWMwYzg2
# ENTRYPOINT ["dotnet", "Microsoft.Azure.Devices.Edge.Azure.Monitor.dll"]

ARG base_tag=3.1.12-alpine3.12
FROM mcr.microsoft.com/dotnet/core/runtime:${base_tag}

ARG EXE_DIR=.

ENV MODULE_NAME "Microsoft.Azure.Devices.Edge.Azure.Monitor.dll"

WORKDIR /app

COPY $EXE_DIR/ ./

# Add an unprivileged user account for running the module
RUN adduser -Ds /bin/sh moduleuser 
USER moduleuser

CMD echo "$(date --utc +"[%Y-%m-%d %H:%M:%S %:z]"): Starting Module" && \
    exec /usr/bin/dotnet Microsoft.Azure.Devices.Edge.Azure.Monitor.dll
