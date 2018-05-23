ARG base_tag=2.1.0-rc1-runtime-bionic-arm32v7
FROM microsoft/dotnet:${base_tag}

# Add an unprivileged user account for running the module
RUN useradd -ms /bin/bash moduleuser
USER moduleuser