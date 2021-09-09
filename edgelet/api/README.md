# How to build Management API using Swagger-Codegen
1. Install Java & Maven 
```sh
   sudo apt-get install openjdk-8-jdk
   ## It only works on JAVA8 see https://computingforgeeks.com/how-to-set-default-java-version-on-ubuntu-debian/ how to switch between Java versions
   sudo apt install maven
```
2. Clone swagger codegen repo:
```sh
   cd ~
   git clone https://github.com/swagger-api/swagger-codegen.git
```
3. Test build if installation was done correctly ( https://github.com/swagger-api/swagger-codegen#building ) 
```sh
   cd swagger-codegen
   mvn clean install -N
   mvn clean package
```
4. Build swagger-codegen
```sh
   cd modules/swagger-codegen
   mvn clean package

   ## Make sure /target/*.jar is created
   ls
```
5. Build swagger-codegen-cli
```sh
   cd ~/swagger-codegen/modules/swagger-codegen-cli
   mvn clean package

   ## Make sure /target/*.jar is created
   ls 
```
6. Put it all together and build API
    - Run the following command 
```sh
		 java -cp <abs/path/to/jar/step4>:<abs/path/to/jar/step5> \
		 io.swagger.codegen.SwaggerCodegen generate \
		 -l rust \
		 -i <abs/path/to/iotedge/mgmt/yaml> \
		 -o <abs/path/to/output/folder>

        ## Example Command
		 java -cp /home/iotedgeuser/swagger-codegen/modules/swagger-codegen/target/swagger-codegen-2.4.20-SNAPSHOT.jar:/home/iotedgeuser/swagger-codegen/modules/swagger-codegen-cli/target/swagger-codegen-cli.jar \
		 io.swagger.codegen.SwaggerCodegen generate \
		 -l rust \
		 -i /home/iotedgeuser/iotedge/edgelet/api/managementVersion_2020_07_07.yaml \
		 -o /home/iotedgeuser/management
```
# Note #
We've manually fixed up the generated code so that it satisfies rustfmt and clippy. As such, if you ever need to run `swagger-codegen-cli` against new definitions, or need to regenerate existing ones, you will want to perform the same fixups manually. Make sure to run clippy and rustfmt against the new code yourself, and inspect the diffs of modified files before checking in.