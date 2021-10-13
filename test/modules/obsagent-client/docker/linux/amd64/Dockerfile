FROM ubuntu:18.04

WORKDIR /app

COPY ./x86_64-unknown-linux-gnu/release/obsagent-client ./

# Add an unprivileged user account for running the module
RUN useradd -ms /bin/bash moduleuser
USER moduleuser

CMD echo "$(date --utc +"[%Y-%m-%d %H:%M:%S %:z]"): Starting Module" && \
    exec /app/obsagent-client