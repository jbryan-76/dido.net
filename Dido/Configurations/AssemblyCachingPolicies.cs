namespace DidoNet
{
    /// <summary>
    /// Policies for caching application assemblies on a remote runner.
    /// </summary>
    public enum AssemblyCachingPolicies
    {
        /// <summary>
        /// Resolves to 'Never' when the application is compiled with a DEBUG configuration,
        /// else resolves to 'Always'.
        /// </summary>
        Auto,

        /// <summary>
        /// Assemblies are cached where possible, depending on the runner configuration.
        /// </summary>
        Always,

        /// <summary>
        /// Assemblies are never cached: each time a task runs on a remote runner, missing assemblies
        /// are requested and transferred from the application domain.
        /// </summary>
        Never
    }
}
