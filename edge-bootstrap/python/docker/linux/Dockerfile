ARG BASE_VERSION=2.7.14-jessie
FROM python:${BASE_VERSION}

ARG EXE_DIR=.

WORKDIR /app

COPY $EXE_DIR/ ./

RUN pip install -U .[test,dev]

CMD ["./scripts/run_tests.sh"]
