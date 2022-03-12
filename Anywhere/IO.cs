namespace AnywhereNET
{
    public static class IO
    {
        // TODO: use this to r/w files on the application server
        // TODO: relative files only and only files that resolve as descendents from the root app folder?
        public static Stream Open(string path)
        {
            // Environment.ApplicationStream
            // TODO: negotiate a network r/w stream to a file?
            throw new NotImplementedException();
        }
    }
}
