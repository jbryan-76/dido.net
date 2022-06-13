namespace DidoNet.IO
{
    public class RunnerDirectoryProxy
    {
        // System.IO.Directory;
        // create; delete; enumerate; exists; get info; enumerate files; 
        //internal MessageChannel? Channel { get; set; }

        internal Connection? Connection { get; set; }

        internal RunnerDirectoryProxy(Connection? connection, RunnerConfiguration? configuration = null, string? applicationId = null)
        {
            Connection = connection;
        }

    }
}
