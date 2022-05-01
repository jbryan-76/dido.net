The Dido library fills a niche in the distributed computing problem space when there is a
desire to develop, test, and deploy a monolithic application that performs CPU- and memory-intensive
calculations but those calculations should not or can not be performed in the machine/environment of
the host application, usually due to resource constraints.

Traditionally the solution is to turn to auxiliary (micro)services, a generic job processing system,
or cloud compute solutions (eg AWS, Azure, GPC). Occasionally the development and administration overhead
associated with these solutions adds undesired complexity or cost for small teams (designing and authoring 
additional services/applications, developing and troubleshooting communications and security protocols, 
migrating and maintaining applications as data models and algorithm needs change, debugging a distributed
system, etc).

The Dido library API is designed to be similar to the .NET Task Parallel library in that it provides a
way to utilize high-performance, distributed processing easily and directly in code, relying on an underlying 
system to handle the complexities of actually running the code in a distributed fashion.

The Dido library seeks to simplify things by allowing the application to be written as a conceptual 
monolith, containing all necessary code to perform all needed calculations. Where distributed/remote 
calculations need to be performed, simply use the Dido API to invoke...TODO

This solution is similar to the legacy/deprecated .NET Remoting or general RPC pattern with a crucial
difference: all code is only contained in the host application - it does not need to be "split" into services
or auxiliary applications or plugins. ...TODO

It works by serializing an expression, transmitting it to another machine,
then deserializing, reconstructing, and invoking the original expression in a different environment.
When the expression has dependencies on assemblies defined or used by the host application, those
assemblies are transmitted from the host to the remote environment and loaded into the remote domain to 
allow the expression to execute. Once execution has completed, the result is transmitted back to the host
application.
