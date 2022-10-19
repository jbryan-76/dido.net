# Basic Sample Application

This sample application performs arbitrary busy work to simply demonstrate how to use Dido to remotely execute code.

*NOTE: these instructions assume a Windows system with .NET 6+ installed. Adjustments may be necessary for other operating systems.*

## Step 1: Build all projects

```
dotnet build -c Release Dido.sln
```

## Step 2: Start a Runner
In one console/command window:
```
Dido.Runner\bin\Release\net6.0\Dido.Runner.exe
```

## Step 3: Run the application
In another console/command window:
```
Samples\SampleApp\bin\Release\net6.0\SampleApp.exe -runner https://localhost:4940
```

