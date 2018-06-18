FROM node:alpine

WORKDIR /app
ADD app.js /app/
ADD package.json /app/
ADD package-lock.json /app/
RUN ["/usr/local/bin/node", "/usr/local/bin/npm", "install"]

ENTRYPOINT ["/usr/local/bin/node", "/app/app.js"]
