FROM alpine:3.13

WORKDIR /app

COPY ./x86_64-unknown-linux-gnu/release/obsagent-client ./

# Add an unprivileged user account for running the module
ARG MODULEUSER_ID=1000
RUN adduser -Ds /bin/sh -u ${MODULEUSER_ID} moduleuser 
USER moduleuser

CMD echo "$(date --utc +"[%Y-%m-%d %H:%M:%S %:z]"): Starting Module" && \
    exec /app/obsagent-client