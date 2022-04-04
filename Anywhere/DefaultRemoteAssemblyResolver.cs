namespace AnywhereNET
{
    public class DefaultRemoteAssemblyResolver
    {
        public static Task<Stream?> ResolveAssembly(Environment env, string assemblyName)
        {
            if (env.ApplicationChannel == null)
            {
                throw new InvalidOperationException($"{nameof(env.ApplicationChannel)} is null");
            }

            // TODO: request the assembly from the application using the channel
            // TODO: receive the assembly and yield it to the caller

            return Task.FromResult(env.ApplicationChannel)!;
        }
    }
}