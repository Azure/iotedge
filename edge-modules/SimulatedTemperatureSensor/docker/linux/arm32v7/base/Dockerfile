ARG base_tag=2.1-runtime-stretch-slim-arm32v7
FROM microsoft/dotnet:${base_tag}

# Add an unprivileged user account for running the module
RUN useradd -ms /bin/bash moduleuser
USER moduleuser