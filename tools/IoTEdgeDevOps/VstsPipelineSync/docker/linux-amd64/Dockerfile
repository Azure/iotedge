ARG base_tag=3.1
FROM mcr.microsoft.com/dotnet/runtime:${base_tag}

ARG EXE_DIR=.

WORKDIR /app

COPY $EXE_DIR/ ./

CMD echo "$(date --utc +"[%Y-%m-%d %H:%M:%S %:z]"): Starting Module" && \
    exec /usr/bin/dotnet VstsPipelineSync.dll "refs/heads/master,refs/heads/release/1.1,refs/heads/release/1.2" "00:03:00"
