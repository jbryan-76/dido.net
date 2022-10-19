# Sample File Compressor Application

This sample application provides a toy example of compressing a file by using the Proxy IO API to cache a file from the application file-system to the Runner local file-system available to the remote code, deflating it, then storing it back on the application file-system.

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

## Step 3: Run the application to compress a file
In another console/command window, create a compressed destination file:
```
SampleFileCompressor\bin\Release\net6.0\SampleFileCompressor.exe https://localhost:4940 SOURCE_FILE OUT_FILE
```

## Step 4: Run the application to decompress a file
In another console/command window, decompress an existing file:
```
SampleFileCompressor\bin\Release\net6.0\SampleFileCompressor.exe https://localhost:4940 OUT_FILE.compressed DESTINATION_FILE
```

