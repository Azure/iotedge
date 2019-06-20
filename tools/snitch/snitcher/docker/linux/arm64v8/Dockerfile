FROM alpine:latest

RUN apk --no-cache add ca-certificates
COPY /snitcher /usr/local/bin/
CMD /usr/local/bin/snitcher