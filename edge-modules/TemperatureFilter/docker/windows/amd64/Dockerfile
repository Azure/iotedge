ARG base_tag=2.1.0-runtime-nanoserver-1803
FROM microsoft/dotnet:${base_tag}

ARG EXE_DIR=.

WORKDIR /app

COPY $EXE_DIR/ ./

CMD echo %DATE:~4,2%-%DATE:~7,2%-%DATE:~10,4% %TIME%: Starting Module && \
    dotnet TemperatureFilter.dll
