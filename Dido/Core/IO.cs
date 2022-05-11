namespace DidoNet
{
    public static class IO
    {
        // TODO: use this to r/w files on the application server
        // TODO: relative files only and only files that resolve as descendents from the root app folder?
        public static Stream Open(string path)
        {
            // System.IO.Directory;
            // create; delete; enumerate; exists; get info; enumerate files; 

            // System.IO.File;
            // append; copy; create; exists; delete; get info; move; open; read; write;

            // Environment.ApplicationStream
            // TODO: negotiate a network r/w stream to a file?
            throw new NotImplementedException();
        }
    }
}
