ARG base_tag=2.1.6-runtime-nanoserver-1809
FROM microsoft/dotnet:${base_tag}

ARG EXE_DIR=.

WORKDIR /app

COPY $EXE_DIR/ ./

USER ContainerAdministrator
# Add an unprivileged user account for running the module
RUN net user /add moduleuser

CMD echo %DATE:~4,2%-%DATE:~7,2%-%DATE:~10,4% %TIME%: Starting Module && \
    dotnet TemperatureFilter.dll
