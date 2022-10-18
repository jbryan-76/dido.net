# Dido
### (Latin) /dēdō/ *verb*: distribute, disseminate, divide, spread.

Dido is a .NET framework to facilitate incorporating distributed computing patterns directly into an application without the overhead of authoring, releasing, or maintaining multiple services or executables. Code can be executed locally within the application domain or remotely in a different environment (or potentially on a different OS), in any combination, using a single, configurable API. In this manner, its goal is similar to the .NET [Task Parallel Library](https://docs.microsoft.com/en-us/dotnet/standard/parallel-programming/task-parallel-library-tpl) (TPL) to easily add parallel and distributed computing capability directly to existing code (often with minimal or no refactoring), but with code execution unconstrained by local system resources.

## DISCLAIMER

This is a **WORK IN PROGRESS** project to evaluate the practical feasibility and associated pros and cons of dynamically transporting code assemblies to facilitate new patterns in distributed computing. Its current state is focused on correctness not performance. It does not necessarily use best practices in all areas, is not fully documented, nor has robust implementations and coverage for all critical communications, security, logging, code execution, and exception handling scenarios. It **SHOULD NOT** be used in production or mission critical applications.

The short term goal is to determine the viability and efficacy of the core concepts and framework by gathering comments and feedback and contributions to improve code quality and features. The long term goal is to hopefully create a robust and simple option for small or constrained teams to utilize advanced distributed computing patterns in .NET applications with an easy-to-use API and easy-to-administer service catalog.

### _Table of Contents_
- [Basic Example](#basic-example)
- [Background](#background)
- [Basic Walkthrough](#basic-walkthrough)
- [Basic API and Typical Use Cases](#basic-api-and-typical-use-cases)
- [Limitations](#limitations)
- [Samples](#samples)
- [Docker](#docker)
- [Technical Discussion](#technical-discussion)
    - [Architecture](#architecture)
    - [Execution Modes](#configurable-execution-modes)
        - [Baseline Execution](#baseline-execution)
        - [Local Execution](#local-execution)
        - [Dedicated Runner Execution](#dedicated-runner-execution)
        - [Clustered Runner Execution](#clustered-runner-execution)
- [Security](#security)
    - [Generating a Self-Signed Certificate](#generating-a-self-signed-certificate)

# Basic Example

```
class MyWork
{
  public static bool DoSomethingLongAndExpensive()
  {
     // TODO: allocate huge amounts of memory, utilize 100% CPU, etc...
     return true;
  }
}

class MyApp
{
  public async Task MyMain()
  {
    var config = new DidoNet.Configuration { /* ...configure... */ };
    // depending on the configuration, the lambda expression below will
    // either run locally or in a generic remote runner service.
    var result = await DidoNet.Dido.RunAsync(
       (context) => MyWork.DoSomethingLongAndExpensive(),
       config
    );
  }
}
```

In this example `MyWork.DoSomethingLongAndExpensive` is a method that takes a long time to complete or requires a large amount of resources. When the method is invoked via Dido in `MyApp.MyMain`, depending on the configuration the method may either run locally within the application process or remotely in a different compute environment. This allows an otherwise monolithic application to easily utilize distributed computing to improve performance with minimal developer effort and overhead.

# Background

At the most basic level, the Dido framework targets the distributed computing problem space where there is a need to develop, test, and deploy an application that performs CPU- and memory-intensive calculations but those calculations should not or can not be performed on the machine or in the environment of the host application, usually due to resource constraints. In more complex scenarios, with proper configuration and an available pool of generic Dido services, other use cases such as a jobs system or a service-oriented system of microservices are also possible from a single governing monolithic application.

The most common traditional solutions for distributed computing problems are to create one or more auxiliary (micro)services, a generic job processing system, or use service-oriented cloud platforms (e.g. AWS, Azure, GCP), where the host application orchestrates and communicates with the auxiliary systems to perform the necessary work. These solutions are undeniably powerful and flexible, but increase overall application complexity and typically require multiple teams and specific developer expertise and IT administrative overhead, such as:
- Experience with distributed communication and synchronization patterns.
- Experience designing, authoring, deploying, and maintaining multiple unique auxiliary services.
- Developing and troubleshooting communications and security protocols.
- Updating, migrating, and maintaining multiple services as data models and algorithm needs change.
- Debugging a large or complex distributed system.

The Dido framework concept offers a potential compromise for *some* of these areas by inverting the traditional approach and allowing the application to be written as a single conceptual monolith, where it explicitly contains all necessary code (models, data structures, algorithms, assemblies, etc) to perform **all** needed work, and where distributed or non-local invocation of that code is desired, a single API call can securely pack and ship the code and any assembly dependencies to a generic .NET host runner service for execution.

This solution is similar to the legacy/deprecated .NET Remoting or general RPC pattern with a crucial difference: all code is specifically and intentionally only authored and contained in the host application - it does not need to be explicitly and proactively "split" into services or auxiliary applications or plugins, it does not require an intermediate compilation or code generation step, and it does not require proactive developer or IT management to handle new code versions. The code can be directly authored, tested, and executed in a single local environment during development, and then implicitly and dynamically executed remotely on one or more distributed generic "runner" services in production.

*Note*: while Dido can be a powerful and flexible solution for some common distributed computing use cases, it is **not** intended to be a panacea, nor replace dedicated and optimized solutions for specific problems. This framework is intended to offer a simple, inexpensive, and self-managed way to support basic distributed computing patterns without the overhead or cost incurred by large teams and cloud service providers.

# Basic Walkthrough

This introductory walkthrough uses the [Basic app (.NET 6)](Samples/SampleApp) example project in a Windows environment, and summarizes a representative approach to using the Dido framework.

## 1) Develop and test your application in Debug mode.

   Write your application normally; For example, classes and models to perform some work:

    static class Work
    {
        public class Result
        {
            public double Duration { get; set; }
            public long Average { get; set; }
        }

        public static Result DoSomethingLongAndExpensive(int sizeInMB)
        {
            var start = DateTime.Now;

            // allocate a big array and fill it with random numbers
            var rand = new Random();
            var numbers = new int[1024 * 1024 * sizeInMB];
            for (int i = 0; i < numbers.Length; i++)
            {
                numbers[i] = rand.Next();
            }

            // sort it
            Array.Sort(numbers);

            // compute the average value
            long average = 0;
            for (int i = 0; i < numbers.Length; i++)
            {
                average += numbers[i];
            }
            average /= numbers.LongLength;

            // return the result
            return new Result
            {
                Duration = (DateTime.Now - start).TotalSeconds,
                Average = average
            };
        }
    }

   And a main program to orchestrate the work:

    public static async Task Main(string[] args)
    {
        var result = Work.DoSomethingLongAndExpensive(64);

        Console.WriteLine($"Result: duration={result.Duration} average={result.Average}");
    }

   At any point Dido can be used as a degenerate "wrapper" to execute code locally (as long as no global configuration has been provided), which yields the same behavior as if Dido was not used:

    public static async Task Main(string[] args)
    {
        var result = await DidoNet.Dido.RunAsync((context) => Work.DoSomethingLongAndExpensive(64));

        Console.WriteLine($"Result: duration={result.Duration} average={result.Average}");
    }
   
   Run the application to confirm it behaves as expected.

## 2) Configure and start a Runner.

   In this example, the Runner is being used as a local console application, which must first be built from source using the [Dido.Runner](Dido.Runner) project. In more advanced scenarios prebuilt Runner binaries can be installed as a service, or packed in a docker container.

   Since communications between applications and Runners (or Mediators) is encrypted using SSL connections, the Runner must be configured to use an existing certificate, which may already exist in your system, or you can [generate a self-signed certificate](#generating-a-self-signed-certificate) for local development or deployment. *Note: robust certificate generation and management and security best-practices are beyond the scope of this document.*

   First, configure the Runner to use a X509 certificate to encrypt communications (this example uses a self-signed certificate in password-protected PKCS#12 format; More advanced options include providing a base-64 encoded certificate or finding a specific existing certificate in the root CA of the system):

   *Runner appsettings.json*
```
{
    "Server": {
        "CertFile": "mycert.pfx",
        "CertPass": "1234",
    },
    "Runner": {
        "Id": "MyTestRunner"
    }
}
```

   Then start the Runner from the console:
```
    Dido.Runner.exe
```
   After the runner starts, make note of the Endpoint the Runner is listening to, e.g. `https://localhost:4940`.
   
## 3) Update your application to use the Runner.

   Create the Dido configuration to connect to the Runner, and provide it to `Dido.RunAsync` to execute the work remotely using the Runner, instead of executing locally:

    public static async Task Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine($"Missing required argument: runner_uri");
            return;
        }

        var runner_uri = new UriBuilder(args[0]).Uri;

        var config = new DidoNet.Configuration
        {
            ServerCertificateValidationPolicy = DidoNet.ServerCertificateValidationPolicies.Thumbprint,
            ServerCertificateThumbprint = "2E51782C63164181EF715A04F505850D1E6C2FFD",
            ExecutionMode = DidoNet.ExecutionModes.Remote,
            RunnerUri = runner_uri
        };

        var result = await DidoNet.Dido.RunAsync((context) => Work.DoSomethingLongAndExpensive(64), config);

        Console.WriteLine($"Result: duration={result.Duration} average={result.Average}");
    }

   In this example, the configuration indicates that Dido should run the provided lambda expression using a specific remote Runner (given by the runner uri provided on the command line), and the SSL certificate for secure connections to the Runner will be validated using the provided certificate thumbprint (which must match the actual certificate thumbprint the Runner is using). More advanced production scenarios can install and validate certificates in the root CA of the machine, so less configuration is required.

## 4) Compile and run your application.

   Build the application normally, then run it while providing the Runner uri:
```
    MyApp.exe https://localhost:4940
```
   As long as the Runner is running and the certificate and configurations are valid and correct, the application "work" will run in the Runner's host machine and environment, not the application's host machine and environment (although note in this example the application and Runner are both using the same localhost environment). To aid local development and debugging, validation can also be set to `ServerCertificateValidationPolicy = DidoNet.ServerCertificateValidationPolicies._SKIP_` which instructs client connections to a server to skip verifying the certificate, and just assume/trust that it is legitimate. **This should not be done in production systems.**

# Basic API and Typical Use Cases

- **Remote asynchronous execution**
  
  The most common and basic use case is to execute an expression and await the result.
  ```
  await Dido.RunAsync((ctx) => myObj.DoWork(), config);
  ```
  As described above, the expression is executed either locally within the current application process or remotely on a Runner service (depending on the configuration). 
  
  For a single Runner, this use case is suitable for offloading expensive and resource-intensive work where the application is submitting and monitoring tasks infrequently and/or serially, or otherwise managing the "load" on the Runner. By default a remote Runner can only handle running a few concurrent tasks at a time (based on the number of CPU cores in the Runner environment). For higher load scenarios, a Mediator and multiple Runners may be needed and/or a task queue configured on the Runner(s) (see below).

  During active development, it is useful to set `config.ExecutionMode` to `ExecutionModes.Local` or `ExecutionModes.DebugLocal` so the expression executes locally, to more easily support debugging and monitoring for correct behavior.

- **Queued remote execution**

  For use cases where the number of requested tasks is greater than what a Runner can handle concurrently, the Runner can be configured to support queuing tasks. Depending on the volume and frequency of incoming tasks, the application may still need to manage the "load" on the Runner. For example:
  
  *Runner appsettings.json:*
  ```
  { 
     ...
     "Runner": {
        ...
        "MaxQueue": "100"
        ...
     }
     ...
  }
  ```

  The Runner will queue incoming task requests and execute them in-order, up to its configured maximum queue length. See the [Runner Configuration](Dido/Configurations/RunnerConfiguration.cs) for more options.

- **Mediator with Runner pool**

  For use cases where a single Runner is insufficient to handle the load of application tasks, or where Runners with different environment capabilities are required, a pool of multiple Runners can be deployed and collectively registered to a single Mediator. In this case, each Runner is configured with a unique id and label, optionally configured with arbitrary tags to help an application filter for and match specific capabilities, and finally configured to connect to and register with a single Mediator. For example:

  *Runner appsettings.json:*
  ```
  { 
     ...
     "Runner": {
        "Id": "abcd-12345",
        "Label": "TestRunner",
        "Tags": [
            "RAM-64", "CPU-16", "USEast1"
        ],
        "MaxQueue": "100"
        "MediatorUri": "https://localhost:4941"
        ...
     }
     ...
  }
  ```

  Applications can then run tasks using a configuration that asks the Mediator to select an appropriate and available matching Runner from the pool:

```
    var config = new DidoNet.Configuration
    {
        ...
        MediatorUri = "https://localhost:4941",
        RunnerOSPlatforms = new[] { OSPlatforms.Linux },
        RunnerTags = new [] { "CPU-16", "USEast1" }
    };

    var result = await DidoNet.Dido.RunAsync((context) => Work.DoSomethingLongAndExpensive(64), config);
```

   In this hypothetical example, the application is requesting to execute a task using a Runner in a Linux environment with tags matching the subset "CPU-16" and "USEast1", which could indicate any Runner in a specific datacenter and with a specific hardware capability.

- **Jobs API with cached results**

  For use cases where an application is executing numerous long-running tasks and the application itself may terminate before one or more tasks complete, the Jobs API provides a simple but limited means of persisting task results within a Mediator until they can be retrieved by the application. The jobs API generally requires at least the following conditions to perform correctly:
  - A Mediator and at least one Runner, operating with consistent and stable uptimes.
  - The application must continue executing for at least a minimum (but non-deterministic) amount of time after submitting a job to allow for any needed assemblies to transfer to the Runner. Alternatively, a "warm" assembly cache on the Runner *may* allow jobs to be submitted and the application to then immediately terminate (since all needed assemblies will be available in the cache).
  - The task code can use neither the File nor Directory disk IO proxy APIs (since the application is not guaranteed to be running to fulfill the IO requests).
  - The application is responsible for "polling" for the results of completed jobs and for deleting jobs once the results are retrieved.

  If these conditions are met, the jobs API can be used to easily incorporate distributed and asynchronous parallel computing with deferred results retrieval in an otherwise monolithic application. The jobs API exposes 4 primary methods:

  - SubmitJobAsync(): Similar to `RunAsync()`, executes an expression as a task on a remote Runner, but stores the result in the Mediator instead of waiting for and yielding it directly to the application.
  - QueryJobAsync(): Retrieves a [JobResult](Dido/Core/JobResult.cs) object containing the current state of a job, including its status and any available result or error.
  - DeleteJobAsync(): Cancels and deletes a previously submitted job. The job is deleted from the store and no object will be returned from `QueryJobAsync()`.
  - CancelJobAsync(): Cancels (but does not delete) a previously submitted job. The job state can be retrieved with `QueryJobAsync()`.

  The Mediator server configuration includes a default in-memory persistence store for job records, but a custom storage solution implementing the [IJobStore](Dido/Interfaces/IJobStore.cs) interface can also be provided, for example to perist job records to a database. See the [Mediator Configuration](Dido/Configurations/MediatorConfiguration.cs) for more options.

# Limitations

This framework currently has the following limitations:

- Communications are encypted and require a valid SSL certificate. There is no option to use non-encrypted communications or username+password or API key authentication.
- Any local disk IO required by the executing task must use the [File Proxy](Dido/IO/RunnerFileProxy.cs) and [Directory Proxy](Dido/IO/RunnerDirectoryProxy.cs) APIs, which are limited replacement implementations of the corresponding .NET System.IO APIs which proxy disk IO requests through the network connection between the Application and Runner.
- Depending on the scope and complexity of a task, for some use cases the application must remain "connected" to a Runner throughout the lifetime of a submitted task. For example, when a task runs, any dependent assemblies it requires are lazy-loaded when the corresponding code executes. If the application connection to the runner is terminated prior to all needed assemblies being transferred or all proxied disk IO operations completing, the task will not complete successfully.
- When a Runner terminates unexpectedly, all in-progress tasks it is running are lost and all active connections to applications broken. Depending on the configuration used in `RunAsync()` and whether the Runner is restarted or another Runner is available, the task may be retried automatically from the application side.
- When a Mediator terminates unexpectedly, all active connections to Runners and applications will be broken, effectively abandoning all in-progress jobs that might be running. Additionally, if the Mediator is using the default in-memory job records store, all in-progress or completed job records and results are lost (however if a custom implementation is used with a persistant backing store, presumably it will retain the job records).

To mitigate some of these limitations, Runners support caching assemblies (with optional encryption). Future possible API improvements also include: "pre-transferring" assemblies and/or files before the task starts; Additional communications and authentication options; "Re-connecting" an application or Runner or Mediator after a disconnection; using a simple disk-based persistant store as the default (such as LiteDB) instead of an in-memory store.

# Samples
- [Basic app (.NET 6)](Samples/SampleApp)
- [Basic app (.NET Core 3.1)](Samples/SampleAppCore3.1)
- [File compressor (.NET 6)](Samples/SampleFileCompressor)
- [Video transcoder (.NET 6)](Samples/SampleVideoTranscoder)

# Docker
Basic [Docker images](docker/README.md) for a Runner Server and Mediator Server can be built from source, but are not currently published to Docker Hub.

# Technical Discussion

## Architecture

![baseline execution](./documentation/images/framework_uml.png)

The Dido framework is implemented in .NET 6.0 and consists of:
- A Library containing the API, configuration models, and key data structures and utilities.
- A Mediator Service which serves as a limited pseudo load balancer and which coordinates access to a pool of generic Runner instances (available as a console app, OS service, or docker image).
- A Runner Service that communicates with the host application (via the Library) and optional Mediator instance to execute application code (available as a console app, OS service, or docker image).

Without a Mediator nor Runner, the Dido API degenerates to local execution, which is no different than executing code without using the framework. Alternatively, exactly one Runner can be used without a Mediator for simple scenarios with limited or more predictable remote computing needs. Finally, a Mediator can be used with one or more Runners for more advanced or demanding distributed computing scenarios.

Although a variety of different use cases are supported via appropriate configuration, the nominal functional operation is represented by the following sequence diagram:

![baseline execution](./documentation/images/run_sequence.png)

1. The application requests to execute an expression using a Dido API method.
2. The Mediator is contacted to find an available Runner.
3. The expression is serialized and transmitted to the Runner.
4. The Runner deserializes the expression and attempts to instantiate and execute it.
5. Inevitably, the expression requires application and dependency assemblies that do not yet exist in the Runner domain (or whose previously cached version has expired), so those assemblies are securely transmitted to the Runner.
6. Once all assemblies are available and loaded, the expression is executed.
7. The expression result is transmitted back to the application.

## Configurable Execution Modes

### Baseline Execution
![baseline execution](./documentation/images/baseline_execution.png)

```
myObj.DoWork();
```

Without using Dido, the application nominally contains code performing some function. This code may simply utilize local resources such as CPU and memory, or may use the file-system, a database, or other connected services.


### Local Execution
![baseline execution](./documentation/images/local_execution.png)

```
config.ExecutionMode = ExecutionModes.Local;
await Dido.RunAsync((ctx) => myObj.DoWork(), config);
```

When a Dido API method is configured for Local Execution, execution of code degenerates to the baseline: it is equivalent to not using Dido at all. This can greatly enhance the ability to develop and debug an application because all code is executing locally; allowing monitoring, breakpoints, etc.


### Dedicated Runner Execution
![baseline execution](./documentation/images/dedicated_execution.png)

```
config.ExecutionMode = ExecutionModes.Remote;
config.RunnerUri = "https://localhost:4940";
await Dido.RunAsync((ctx) => myObj.DoWork(ctx), config);
```

When a Dido API method is configured for Remote Execution with a single Runner, the code is executed remotely. Depending on the Runner environment, this may mean access to more (or at least dedicated) resources than is available in the host application environment. As long as the application code is parameterized with connection strings or other necessary credentials, and the Runner environment properly configured to allow those network connections, the code can access databases and other services normally, with no special handling. However, file-system access must use proxy IO instances exposed by the Dido runtime execution context which wrap underlying connections to properly marshal data between the Runner and host application file-system.

### Clustered Runner Execution
![baseline execution](./documentation/images/cluster_execution.png)

```
config.ExecutionMode = ExecutionModes.Remote;
config.MediatorUri = "https://localhost:4940";
await Dido.RunAsync((ctx) => myObj.DoWork(ctx), config);
```

When a Dido API method is configured for Remote Execution with a Mediator, the code is executed remotely using the best available Runner that matches configured filter criteria, but otherwise identical to the dedicated Runner scenario described above. When Runners are deployed using containers and paired with appropriate monitoring and auto-scaling solutions such as Kubernetes, the Runner pool can theoretically dynamically adjust to load conditions from one or more applications, with no configuration nor code changes required by the application.

## Runner Configuration

See the [Runner Configuration](Dido/Configurations/RunnerConfiguration.cs) class for more information about configuring a Runner Server.

## Mediator Configuration

See the [Mediator Configuration](Dido/Configurations/MediatorConfiguration.cs) class for more information about configuring a Mediator Server.

# Security
Communications between the application and a Runner or Mediator instance is encrypted using SSL certificates and .NET SslStreams. Either self-signed or CA-issued certificates can be used, and several options are available in the respective Runner and Mediator configurations to select and use a certificate.

*Note: robust certificate generation and management and security best-practices are beyond the scope of this document.*

Since Runners necessarily load and execute assemblies transferred from the host application and its environment, the Runner environment should not be considered "secure" if multiple applications from different actors are using the same Runner; In principle, the entire Runner environment is available to any code running in a Runner, including assemblies from other applications. However, the [Runner Configuration](#runner-configuration) supports options for encrypting on-disk cached assemblies to minimize casual introspection of potentially proprietary bytecode for Runners running potentially hostile application code. Containerized runners or dedicated environments can be used if additional isolation and security is required.

*Note: robust security and virtualization best-practices are beyond the scope of this document.*

## Generating a Self-Signed Certificate

The following steps use [OpenSSL](https://www.openssl.org/) (LTS v1.1.1) to generate a self-signed certificate, which is available on all major operating systems. Note this is just one basic example; *Robust certificate generation and management and general security best-practices are beyond the scope of this document*. 

1. Generate a new X509 certificate that is valid for 365 days stored in *mycert.pem* and using a new 2048 bit RSA private key stored in *mycert.key*.
```
openssl req -newkey rsa:2048 -new -nodes -keyout mycert.key -x509 -days 365 -out mycert.pem
```
*NOTE*: OpenSSL will ask for several properties when creating the certificate. When providing the *FQDN/Common Name* value, use the exact domain name where the server/service protected by the certificate is addressable. For example, local testing on a single machine would use "localhost".

2. Convert the certificate to PKCS#12/PFX format (which is easily used with .NET SslStream) stored in *mycert.pfx* and protect it with password *1234*.
```
openssl pkcs12 -export -out mycert.pfx -inkey mycert.key -in mycert.pem -password pass:1234
```

3. *[OPTIONAL]* Get the SHA1 certificate thumbprint/fingerprint to use in a Dido.Runner or Dido.Mediator configuration:
```
openssl x509 -noout -fingerprint -sha1 -in mycert.pem
```
or
```
openssl pkcs12 -in mycert.pfx -nodes -passin pass:1234 | openssl x509 -sha1 -noout -fingerprint
```
NOTE: The fingerprint will be reported as a series of 2-byte HEX digits, for example 
`SHA1 Fingerprint=2E:51:78:2C:63:16:41:81:EF:71:5A:04:F5:05:85:0D:1E:6C:2F:FD`, but when used
in a configuration to validate a certificate (e.g. `DidoNet.Configuration.ServerCertificateThumbprint`), the thumbprint must be in a condensed form with no colons, 
for example `2E51782C63164181EF715A04F505850D1E6C2FFD`.

4. Use the certificate file *mycert.pfx* and password *1234* in a Dido.Runner or Dido.Mediator appsettings.json configuration:

*Runner or Mediator appsettings.json*
```
{
    "Server": {
        "CertFile": "mycert.pfx",
        "CertPass": "1234",
    }
    ...
}
```

# TODO
Depending on the viability and level of interest for this project, future enhancements may include:
- Add support for custom communication channels that can be created by the calling application and its remotely executed code to allow two-way communication during task execution on a Runner.
- Add support for progress monitoring during execution of a task, e.g. to update a UI.
- Add configuration support to "pre-transfer" all needed assemblies and files with the initial task request to prevent task failure if the application terminates.
- Add support to "re-connect" to an in-progress task on a Runner if the connection terminates prematurely.
- Explore a more robust and mature workflow that would allow long-running remotely executing code to be dynamically "updated" in-situ when the application source changes. E.g. when the application source is updated and the application restarted, it could re-connect to a running task and that task could save its state, terminate, restart with the new, up-to-date assemblies, load its state, and resume operation.