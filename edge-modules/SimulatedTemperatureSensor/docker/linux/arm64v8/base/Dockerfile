ARG base_tag=3.0-runtime-bionic-arm64v8
FROM microsoft/dotnet:${base_tag}

# Add an unprivileged user account for running the module
RUN useradd -ms /bin/bash moduleuser
USER moduleuser