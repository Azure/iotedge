FROM microsoft/dotnet:2.0.0-preview1-sdk

# Install Java.
WORKDIR /usr/local/java
RUN \
  wget -c --header "Cookie: oraclelicense=accept-securebackup-cookie"  \
    http://download.oracle.com/otn-pub/java/jdk/8u131-b11/d54c1d3a095b4ff2b6607d096fa80163/jdk-8u131-linux-x64.tar.gz && \
  tar zxvf jdk-8u131-linux-x64.tar.gz && \
  rm -rf jdk-8u131-linux-x64.tar.gz && \
  update-alternatives --install "/usr/bin/java" "java" "/usr/local/java/jdk1.8.0_131/jre/bin/java" 1 && \
  update-alternatives --install "/usr/bin/javac" "javac" "/usr/local/java/jdk1.8.0_131/bin/javac" 1 && \
  update-alternatives --install "/usr/bin/javaws" "javaws" "/usr/local/java/jdk1.8.0_131/jre/bin/javaws" 1 && \
  update-alternatives --set java /usr/local/java/jdk1.8.0_131/jre/bin/java && \
  update-alternatives --set javac /usr/local/java/jdk1.8.0_131/bin/javac && \
  update-alternatives --set javaws /usr/local/java/jdk1.8.0_131/jre/bin/javaws

