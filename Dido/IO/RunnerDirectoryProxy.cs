namespace DidoNet.IO
{
    /// <summary>
    /// Provides a limited replica API for System.IO.Directory which is implemented over a network
    /// connection from the ExecutionContext of a Runner to a remote application.
    /// </summary>
    public class RunnerDirectoryProxy
    {
        // TODO: System.IO.Directory;
        // create; delete; enumerate; exists; get info; enumerate files; 
        //internal MessageChannel? Channel { get; set; }

        internal Connection? Connection { get; set; }

        internal RunnerDirectoryProxy(Connection? connection, RunnerConfiguration? configuration = null, string? applicationId = null)
        {
            Connection = connection;
        }

    }
}
