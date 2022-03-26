The Anywhere library fills a niche in the distributed computing problem space when there is a
desire to develop, test, and deploy a monolithic application that performs CPU- and memory-intensive
calculations but those calculations should not or can not be performed in the machine/environment of
the host application, usually due to resource constraints.

Traditionally the solution is to turn to auxiliary (micro)services, a generic job processing system,
or cloud compute solutions (eg AWS, Azure, GPC). Occasionally the development and administration overhead
associated with these solutions adds undesired complexity or cost for small teams (designing and authoring 
additional services/applications, developing and troubleshooting communications and security protocols, 
migrating and maintaining applications as data models and algorithm needs change, debugging a distributed
system, etc).

The Anywhere library API is designed to be similar to the .NET Task Parallel library in that it provides a
way to utilize high-performance, distributed processing easily and directly in code, relying on an underlying 
system to handle the complexities of actually running the code in a distributed fashion.

The Anywhere library seeks to simplify things by allowing the application to be written as a conceptual 
monolith, containing all necessary code to perform all needed calculations. Where distributed/remote 
calculations need to be performed, simply use the Anywhere API to invoke...TODO

This solution is similar to the legacy/deprecated .NET Remoting or general RPC pattern with a crucial
difference: all code is only contained in the host application - it does not need to be "split" into services
or auxiliary applications or plugins. ...TODO

It works by serializing a lambda expression, transmitting it to another machine,
then deserializing, reconstructing, and invoking the original expression in a different environment.
When the expression has dependencies on assemblies defined or used by the host application, those
assemblies are transmitted from the host to the remote environment and loaded into the remote domain to 
allow the expression to execute. Once execution has completed, the result is transmitted back to the host
application.

---UNIT TEST NOTES---

Recent .NET versions deprecated AppDomain, so there is no longer a clean way
to load assemblies into sandboxed containers in the same process such that certain framework API 
methods will be able to "see" the assemblies. For example, AssemblyLoadContext can be used to 
create assembly sandboxes, but Assembly.Load() and Activator.CreateInstance() cannot "see" those
assemblies, which also means libraries such as Newtonsoft.Json won't be able to deserialize objects
using types within those assemblies.

This means that the only way to adequately test some aspects of this library is to use multiple
unit test projects, with some projects directly including sample model assemblies to simulate the 
host application, and other projects loading those assemblies dynamically to simulate the remote 
environments.
