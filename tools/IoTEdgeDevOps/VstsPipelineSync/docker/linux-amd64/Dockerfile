ARG base_tag=3.1
FROM mcr.microsoft.com/dotnet/core/runtime:${base_tag}

ARG EXE_DIR=.

WORKDIR /app

COPY $EXE_DIR/ ./

CMD echo "$(date --utc +"[%Y-%m-%d %H:%M:%S %:z]"): Starting Module" && \
    exec /usr/bin/dotnet VstsPipelineSync.dll  "$TEST_DASHBOARD_VSTS_PAT" "$TEST_DASHBOARD_DB_CONNECTION_STRING" 00:01:00 refs/heads/master,refs/heads/release/1.0.9,refs/heads/release/1.0.8
