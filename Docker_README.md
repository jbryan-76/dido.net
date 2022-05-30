### Create Dido.Runner image by building from source
` docker build -f Dockerfile.Runner.build -t dido.runner . `

### Create Dido.Runner image from existing binaries
` dotnet publish -c Release `
` docker build -f Dockerfile.Runner.pack -t dido.runner . `

### Run a Dido.Runner image, forwarding the default dido port (4940)
` docker run -d -p 4940:4940 dido.runner ` 

### Run a Dido.Runner image, configuring the runner to use a custom port, and forwarding that port
` docker run -d -p 4941:4941 --env Server__Port=4941 dido.runner ` 

