FROM arm64v8/ubuntu:18.04

RUN apt-get update && apt-get install -y libssl-dev 

# Add an unprivileged user account for running the module
RUN useradd -ms /bin/bash moduleuser
USER moduleuser

WORKDIR /app

COPY ./aarch64-unknown-linux-gnu/release/obsagent-client ./

USER moduleuser

CMD echo "$(date --utc +"[%Y-%m-%d %H:%M:%S %:z]"): Starting Module" && \
    exec /app/obsagent-client
