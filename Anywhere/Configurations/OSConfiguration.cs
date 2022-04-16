namespace DidoNet
{
    public static class OSConfiguration
    {
        // TODO: should this be resolvable at runtime? Can it change between win/linux/osx?
        /// <summary>
        /// The extension used for .NET assemblies.
        /// </summary>
        public static readonly string AssemblyExtension = "dll";
    }
}
