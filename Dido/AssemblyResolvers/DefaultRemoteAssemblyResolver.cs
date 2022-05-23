namespace DidoNet
{
    public class DefaultRemoteAssemblyResolver
    {
        private MessageChannel Channel { get; set; }

        public DefaultRemoteAssemblyResolver(MessageChannel channel)
        {
            Channel = channel;
        }

        public Task<Stream?> ResolveAssembly(Environment env, string assemblyName)
        {
            // request the assembly from the application
            Channel.Send(new AssemblyRequestMessage(assemblyName));

            // TODO: switch to a chunked approach to incrementally send the data,
            // TODO: since some assemblies will be large

            // receive the response
            var message = Channel.ReceiveMessage();

            switch (message)
            {
                case AssemblyResponseMessage response:
                    // the current caller will dispose the stream, so wrap the response
                    // in another stream to keep the channel open
                    // TODO: use a different pattern. byte[] all the way?
                    return Task.FromResult<Stream?>(new MemoryStream(response.Bytes));

                case AssemblyErrorMessage error:
                default:
                    return Task.FromResult<Stream?>(null);
            }
        }
    }
}