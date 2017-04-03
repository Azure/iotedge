#!/bin/bash

checkEnvVar() {
	varname=$1
	if [ -z "${!varname}" ]; then
		echo Error: Environment variable $varname is not set 1>&2
		exit 1
	fi
}

# Check if Environment variables are set.
checkEnvVar BUILD_BINARIESDIRECTORY

OUTPUT_FOLDER=$BUILD_BINARIESDIRECTORY
DOCKER_IMAGENAME=azedge
BASEDIR=$(dirname $0)

usage() 
{ 
	echo "Missing arguments. Usage: $0 -r <registry> -u <username> -p <password> [-i <docker image name=azedge>] [-v <docker image version=build number]" 1>&2; 
	exit 1; 
}

while getopts ":r:u:p:i:v:" o; do
    case "${o}" in
        r)
            DOCKER_REGISTRY=${OPTARG}
            ;;
        u)
            DOCKER_USERNAME=${OPTARG}
            ;;
        p)
            DOCKER_PASSWORD=${OPTARG}
            ;;
        i)
            DOCKER_IMAGENAME=${OPTARG}
            ;;        
		v)
            DOCKER_IMAGEVERSION=${OPTARG}
            ;;
		*)
            usage
            ;;
    esac
done
shift $((OPTIND-1))

if [ -z "${DOCKER_REGISTRY}" ] || [ -z "${DOCKER_USERNAME}" ] || [ -z "${DOCKER_PASSWORD}" ]; then
    usage
fi

if [ -z "${DOCKER_IMAGEVERSION}" ]; then 
	if [ ! -z "${BUILD_BUILDNUMBER}" ]; then
		DOCKER_IMAGEVERSION=$BUILD_BUILDNUMBER
	else
		echo Error: Docker image version not found. Either set BUILD_BUILDNUMBER environment variable, or pass in -v parameter.
		exit 1
	fi
fi

if [ ! -d "$BUILD_BINARIESDIRECTORY" ]; then
	echo Path $BUILD_BINARIESDIRECTORY does not exist 1>&2
	exit 1
fi

# Build docker image and push it to private repo
cp $BASEDIR/Dockerfile $OUTPUT_FOLDER
cd $OUTPUT_FOLDER

echo Building Docker image
docker build -t $DOCKER_IMAGENAME:$DOCKER_IMAGEVERSION .

echo Logging in to Docker registry
docker login $DOCKER_REGISTRY -u $DOCKER_USERNAME -p $DOCKER_PASSWORD

echo Pushing Docker image to registry
docker tag $DOCKER_IMAGENAME:$DOCKER_IMAGEVERSION $DOCKER_REGISTRY/$DOCKER_IMAGENAME:$DOCKER_IMAGEVERSION
docker push $DOCKER_REGISTRY/$DOCKER_IMAGENAME:$DOCKER_IMAGEVERSION

exit 0

