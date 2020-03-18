# Base image for arm32v7 dotnet-runtime container
FROM mcr.microsoft.com/windows/nanoserver:1809-arm32v7

ENV DOTNET_VERSION 2.1.13
ENV DOTNET_DOWNLOAD_URL https://dotnetcli.blob.core.windows.net/dotnet/Runtime/$DOTNET_VERSION/dotnet-runtime-${DOTNET_VERSION}-win-arm.zip
 
RUN curl -SL %DOTNET_DOWNLOAD_URL% --output dotnet.zip \
    && mkdir "c:/program files/dotnet" \
    && tar -zxf dotnet.zip -C "c:/program files/dotnet" \
    && del dotnet.zip
 
# In order to set system PATH, ContainerAdministrator must be used
USER ContainerAdministrator
RUN setx /M PATH "%PATH%;C:\Program Files\dotnet"
USER ContainerUser
 
# Configure Kestrel web server to bind to port 80 when present
ENV ASPNETCORE_URLS=http://+:80
# Enable detection of running in a container1
ENV DOTNET_RUNNING_IN_CONTAINER=true

# Don't need to install RocksDB since RocksDB.dll ARM32 version is copied to native/arm folder during build.