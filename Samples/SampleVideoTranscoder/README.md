# Sample Video Transcoder Application

This sample application provides a toy example of transcoding a video file using [FFMpeg](https://ffmpeg.org/).

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

## Step 3: Obtain an ffmpeg executable
Download a precompiled static build of ffmpeg.exe and place in the root folder where the application will be executed (i.e. the current directory if invoking from the command line).

## Step 4: Run the application
In another console/command window, transcode a video file from one format to another:
```
Samples\SampleVideoTranscoder\bin\Release\net6.0\SampleVideoTranscoder.exe https://localhost:4940 SOURCE_VIDEO DESTINATION_VIDEO
```
