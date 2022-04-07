namespace AnywhereNET
{
    public class DefaultRemoteAssemblyResolver
    {
        private Channel Channel { get; set; }

        public DefaultRemoteAssemblyResolver(Channel channel)
        {
            Channel = channel;
            Channel.BlockingReads = true;
        }

        public Task<Stream?> ResolveAssembly(Environment env, string assemblyName)
        {
            // request the assembly from the application
            var request = new AssemblyRequestMessage(assemblyName);
            request.Write(Channel);

            // receive the response
            var response = new AssemblyResponseMessage();
            response.Read(Channel);

            // the current caller will dispose the stream, so we need to wrap in another stream
            // to keep the channel open
            // TODO: use a different pattern. byte[] all the way?
            return response.ContentType == AssemblyResponseMessage.ContentTypes.Error
                ? Task.FromResult<Stream?>(null)
                : Task.FromResult<Stream?>(new MemoryStream(response.Bytes));
        }
    }
}