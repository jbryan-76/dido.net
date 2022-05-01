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
