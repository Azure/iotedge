FROM python:2.7.14-windowsservercore-1709

ARG EXE_DIR=.

WORKDIR /app

COPY $EXE_DIR/ ./

RUN pip install -U .[test,dev]

CMD [".\\scripts\\run_tests.bat --no-coverage"]
