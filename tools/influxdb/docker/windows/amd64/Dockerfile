FROM microsoft/dotnet:nanoserver as core
FROM mcr.microsoft.com/dotnet/core/runtime:2.1.10-nanoserver-1809

# There is a workaround to copy the missing netapi32.dll into dotnet core runtime image. 
COPY --from=core /windows/system32/netapi32.dll /windows/system32/netapi32.dll

ARG EXE_DIR=.

WORKDIR /app

COPY $EXE_DIR/influxd.exe ./

EXPOSE 8086

CMD ["influxd.exe"]