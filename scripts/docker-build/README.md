# How to build

This Dockerfile allows us to build an image that we can then use to build the
Edge code. Build the image with something like:

```
docker build -t edge-build .
```

And then if you have your source checked out to `c:\code\Azure-IoT-Edge-Core`
then you can build it with:

```
docker run --rm -it -v /c/code:/code -w /code/Azure-IoT-Edge-Core edge-build ./scripts/linux/buildBranch.sh
```