### Dido (Latin) /dēdō/ *verb*: distribute, disseminate, divide, spread.

# SUMMARY

Dido is a .NET framework to facilitate incorporating distributed computing patterns directly within an application without the overhead of authoring, releasing, or maintaining multiple services. Code can be executed locally within the application domain or remotely in a different environment or OS, in any combination, using a single, configurable API. In this manner, its goal is similar to the .NET [Task Parallel Library](https://docs.microsoft.com/en-us/dotnet/standard/parallel-programming/task-parallel-library-tpl) (TPL) to easily add parallel/distributed computing capability directly to existing code, often with no refactoring.

# BASIC EXAMPLE

```
class MyWork
{
  public static bool DoSomethingLongAndExpensive()
  {
     // allocate huge amounts of memory, saturate the CPU, etc...
     return true;
  }
}

class MyApp
{
  public async Task MyMain()
  {
    var config = new DidoNet.Configuration { /* ...configure... */ };
    // do the work. depending on the configuration, the code will either run
    // locally on in a generic remote runner service.
    var result = await DidoNet.Dido.RunAsync(
       (context) => MyWork.DoSomethingLongAndExpensive(),
       config
	);
  }
}
```

# BACKGROUND

The Dido framework targets the distributed computing problem space where there is a need to develop, test, and deploy an application that performs CPU- and memory-intensive calculations but those calculations should not or can not be performed on the machine or in the environment of the host application, usually due to resource constraints.

The most common traditional solutions are to create one or more auxiliary (micro)services, a generic job processing system, or use cloud platforms (eg AWS, Azure, GCP), where the host application orchestrates and communicates with the auxiliary systems to perform the necessary work. These solutions are powerful and flexible, but increase overall application complexity and typically require additional development expertise and administrative overhead, such as:
- Experience with distributed communication and synchronization patterns.
- Experience designing and authoring auxiliary services.
- Developing and troubleshooting communications and security protocols.
- Updating, migrating, and maintaining multiple services as data models and algorithm needs change.
- Debugging a large or complex distributed system.

The Dido framework offers a solution that inverts the traditional approach by allowing the application to be written as a single conceptual monolith, where it explicitly contains all necessary code (models, data structures, algorithms, assemblies, etc) to perform all needed work, and where distributed or non-local invokation of that code is desired, a single API call can securely pack and ship the code to a generic .NET host runner service for execution.

This solution is similar to the legacy/deprecated .NET Remoting or general RPC pattern with a crucial difference: all code is specifically and intentionally only authored and contained in the host application - it does not need to be explicitly and proactively "split" into services or auxiliary applications or plugins, and does not require an intermediate compilation or code generation step. The code can be directly authored, tested, and executed in the application during development, and then implicitly and dynamically executed remotely in production.

# TECHNICAL DISCUSSION

### Architecture

![baseline execution](./documentation/images/framework_uml.png)

The Dido framework is implemented in .NET 6.0 and consists of:
- A Library containing the API, configuration models, and key data structures and utilities.
- A Mediator Service which serves as a pseudo load balancer and which coordinates access to a pool of generic runner instances.
- Generic and identical Runner Service(s) that communicate with the host application (via the Library) and optional mediator instance to execute application code.

Without a mediator nor runner, the Dido API degenerates to local execution, which is no different than executing code without using the framework. Alternatively exactly one runner can be used without a mediator for simple scenarios with limited or more predictable remote computing needs. Finally, a mediator can be used with one or more runners for more advanced or demanding distributed computing scenarios.

Although a variety of different use cases are supported via appropriate configuration, the nominal functional operation is represented by the following sequence diagram:

![baseline execution](./documentation/images/run_sequence.png)

1. The application requests to execute an expression using a Dido API method.
2. The Mediator is contacted to find an available runner.
3. The expression is serialized and transmitted to the runner.
4. The runner deserializes the expression and attempts to instantiate and execute it.
5. Inevitably, the expression requires application and dependency assemblies that do not yet exist in the runner domain (or whose version is different), so those assemblies are securely transmitted to the runner.
6. Once all assemblies are available and loaded, the expression is executed.
7. The expression result is transmitted back to the application.

## Configurable Execute Modes

### Baseline Execution
![baseline execution](./documentation/images/baseline_execution.png)

```
myObj.DoWork();
```

Without using Dido, the application nominally contains code performing some function. This code may simply utilize local resources such as CPU and memory, or may use the filesystem, a database, or other connected services.


### Local Execution
![baseline execution](./documentation/images/local_execution.png)

```
config.ExecutionMode = ExecutionModes.Local;
Dido.RunAsync((ctx) => myObj.DoWork(), config);
```

When a Dido API method is configured for Local Execution, execution of code degenerates to the baseline: it is equivalent to not using Dido at all. This can greatly enhance the ability to develop and debug an application because all code is executing locally; allowing monitoring, breakpoints, etc.


### Dedicated Runner Execution
![baseline execution](./documentation/images/dedicated_execution.png)

```
config.ExecutionMode = ExecutionModes.Remote;
config.RunnerUri = "https://localhost:4940";
Dido.RunAsync((ctx) => myObj.DoWork(ctx), config);
```

When a Dido API method is configured for Remote Execution with a single runner, the code is executed remotely. Depending on the runner environment, this may mean access to more (or at least dedicated) resources than is available in the host application environment. As long as the application code is parameterized with connection strings or other necessary credentials, and the runner environment properly configured to allow those network connections, the code can access databases and other services normally, with no special handling. However, filesystem access must use proxy IO instances exposed by the Dido runtime execution context which wrap underlying connections to properly marshal data between the runner and host application filesystem.

### Clustered Runner Execution
![baseline execution](./documentation/images/cluster_execution.png)

```
config.ExecutionMode = ExecutionModes.Remote;
config.MediatorUri = "https://localhost:4940";
Dido.RunAsync((ctx) => myObj.DoWork(ctx), config);
```

When a Dido API method is configured for Remote Execution with a mediator, the code is executed remotely using the best available runner that matches configured filter criteria, but otherwise identical to the dedicated runner use case described above. When paired with appropriate monitoring and auto-scaling solutions such as Kubernetes, the runner pool can dynamically adjust to load conditions from one or more applications, with no configuration nor code changes required by the application.

### Use Cases

- Immediate asynchronous remote execution: TODO
- Deferred asynchronous remote execution with callback: TODO
- Queued remote execution: TODO
- Job management system with persisted results: TODO


### Security
TODO

### Advanced
TODO