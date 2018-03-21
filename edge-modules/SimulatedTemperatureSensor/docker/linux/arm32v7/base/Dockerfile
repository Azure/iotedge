ARG base_tag=2.0.6-runtime-stretch-arm32v7
FROM microsoft/dotnet:${base_tag}

# Add an unprivileged user account for running the module
RUN useradd -ms /bin/bash moduleuser
USER moduleuser