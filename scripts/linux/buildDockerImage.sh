#!/bin/bash

OUTPUT_FOLDER=$BUILD_BINARIESDIRECTORY
DOCKER_IMAGENAME=azedge
DOCKER_IMAGEVERSION=$BUILD_BUILDNUMBER
BASEDIR=$(dirname $0)

usage() 
{ 
	echo "Missing arguments. Usage: $0 -r <registry> -u <username> -p <password>" 1>&2; 
	exit 1; 
}

while getopts ":r:u:p:" o; do
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
        *)
            usage
            ;;
    esac
done
shift $((OPTIND-1))

if [ -z "${DOCKER_REGISTRY}" ] || [ -z "${DOCKER_USERNAME}" ] || [ -z "${DOCKER_PASSWORD}" ]; then
    usage
fi

echo BUILD_BINARIESDIRECTORY = $OUTPUT_FOLDER

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

