FROM mcr.microsoft.com/dotnet/core/sdk:3.1-nanoserver-1809 AS build-env
WORKDIR /app

# COPY *.csproj ./
# RUN dotnet restore

# copy and publish app and libraries
# COPY . /tmp/pre-copy
# RUN if exist c:\\tmp\\pre-copy\\bin rmdir c:\\tmp\\pre-copy\\bin
# RUN if exist c:\\tmp\\pre-copy\\obj rmdir c:\\tmp\\pre-copy\\obj
# RUN move c:\\tmp\\pre-copy\\* c:\\app

COPY * ./
RUN dotnet restore

RUN dotnet publish -c Release -o out

FROM mcr.microsoft.com/dotnet/core/runtime:3.1-nanoserver-1809
WORKDIR /app
COPY --from=build-env /app/out ./

# add Application Insights instrumentation key into build
ENV APPINSIGHTS_INSTRUMENTATIONKEY=NWQzN2MxYTQtYWZmOS00NmFhLTllMTgtZDA1ODZhOWMwYzg2

ENTRYPOINT ["dotnet", "Microsoft.Azure.Devices.Edge.Azure.Monitor.dll"]
