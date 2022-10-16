# Docker Examples

The following examples use simple docker files to build images either from source or from pre-existing binaries. 

-----------------

## Dido.Runner

### Create a Dido.Runner image by building from source
` docker build -f Dockerfile.Runner.build -t dido.runner . `

### Create a Dido.Runner image from existing binaries
```
dotnet publish -c Release
docker build -f Dockerfile.Runner.pack -t dido.runner . 
```

### Run a Dido.Runner image, forwarding the default dido port (4940)
` docker run -d -p 4940:4940 dido.runner ` 

### Run a Dido.Runner image, configuring and forwarding a custom port
` docker run -d -p 4941:4941 --env Server__Port=4941 dido.runner ` 

-----------------

## Dido.Mediator

### Create a Dido.Mediator image by building from source
` docker build -f Dockerfile.Mediator.build -t dido.mediator . `

### Create a Dido.Mediator image from existing binaries
```
dotnet publish -c Release
docker build -f Dockerfile.Mediator.pack -t dido.mediator . 
```

### Run a Dido.Mediator image, forwarding the default dido port (4940)
` docker run -d -p 4940:4940 dido.mediator ` 

### Run a Dido.Mediator image, configuring and forwarding a custom port
` docker run -d -p 4941:4941 --env Server__Port=4941 dido.mediator ` 
