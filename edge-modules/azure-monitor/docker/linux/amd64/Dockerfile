FROM mcr.microsoft.com/dotnet/core/sdk:3.1-buster AS build-env
WORKDIR /app

COPY *.csproj ./
RUN dotnet restore

# COPY . ./
COPY . /tmp/pre-copy
RUN rm -rf /tmp/pre-copy/bin /tmp/pre-copy/obj
RUN mv /tmp/pre-copy/* ./

RUN /app/write_vernum_print_script.sh

RUN dotnet publish -c Release -o out

FROM mcr.microsoft.com/dotnet/core/runtime:3.1-buster-slim
WORKDIR /app
COPY --from=build-env /app/out ./

COPY --from=build-env /app/get_vernum.sh ./get_vernum.sh

RUN useradd -ms /bin/bash moduleuser
USER moduleuser

# add Application Insights instrumentation key into build
ENV APPINSIGHTS_INSTRUMENTATIONKEY=NWQzN2MxYTQtYWZmOS00NmFhLTllMTgtZDA1ODZhOWMwYzg2
ENTRYPOINT ["dotnet", "Microsoft.Azure.Devices.Edge.Azure.Monitor.dll"]
