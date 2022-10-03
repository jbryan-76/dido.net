﻿using System;

namespace DidoNet
{
    /// <summary>
    /// Configures a runner.
    /// </summary>
    public class RunnerConfiguration
    {
        /// <summary>
        /// The unique id of the server instance.
        /// If not provided, a random unique id is used.
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// The optional label for the runner. 
        /// When submitting a task request this can be used to select a specific runner.
        /// </summary>
        public string Label { get; set; } = string.Empty;

        /// <summary>
        /// The set of optional tags for the runner.
        /// When submitting a task request this can be used to filter runners.
        /// </summary>
        public string[] Tags { get; set; } = new string[0];

        /// <summary>
        /// The maximum number of tasks to execute concurrently.
        /// For best performance, this should roughly correlate to the number of CPU cores available.
        /// <para/>Legal values are:
        /// <para/>Less than or equal to zero (default) = Auto (will be set to the available 
        /// number of cpu cores present on the system).
        /// <para/>Anything else indicates the maximum number of tasks.
        /// </summary>
        public int MaxTasks { get; set; } = 0;

        /// <summary>
        /// The maximum number of pending tasks to accept and queue before rejecting.
        /// <para/>Legal values are:
        /// <para/>Less than zero = Unlimited (up to the number of simultaneous connections allowed by the OS).
        /// <para/>Zero (default) = Tasks cannot be queued. New tasks are accepted only if fewer than
        /// the maximum number of concurrent tasks are currently running.
        /// <para/>Anything else indicates the maximum number of tasks to queue.
        /// </summary>
        public int MaxQueue { get; set; } = 0;

        /// <summary>
        /// The uri for the mediator service used to monitor and manage runners.
        /// If null, the runner will operate in an independent/isolated mode.
        /// </summary>
        public string? MediatorUri { get; set; } = null;

        /// <summary>
        /// The uri for applications to use to connect to a runner.
        /// Setting this explicitly may be necessary for proper routing when
        /// load balancers and network translation services are in use.
        /// If not provided, will default to the endpoint (ip address + port) the 
        /// runner server starts on.
        /// </summary>
        public string? Endpoint { get; set; } = null;

        /// <summary>
        /// The validation policy for authenticating the remote server certificate for SSL connections.
        /// </summary>
        public ServerCertificateValidationPolicies ServerValidationPolicy { get; set; } = ServerCertificateValidationPolicies.RootCA;

        /// <summary>
        /// For ServerCertificateValidationPolicies.Thumbprint, the specific certificate thumbprint to validate against.
        /// </summary>
        public string ServerCertificateThumbprint { get; set; } = string.Empty;

        /// <summary>
        /// The relative or absolute path on the runner's local file-system where proxied application
        /// files and assemblies requested by an executing task are cached.
        /// If not specified, a suitable default relative to the runner's executing path is used.
        /// <para/>NOTE: Cached files for the runner will be stored in a subdirectory of the CachePath
        /// named for the configured unique Id of the runner server instance. This is done to eliminate potential
        /// collisions if multiple runner server instances are using the same root cache.
        /// </summary>
        public string CachePath { get; set; } = "cache";

        /// <summary>
        /// The maximum age for a cached file before it is deleted or replaced.
        /// A timespan less than or equal to zero indicates cached files never expire.
        /// </summary>
        public TimeSpan CacheMaxAge { get; set; } = TimeSpan.Zero;

        /// <summary>
        /// Indicates whether the runner's specific cached files will be deleted when the runner server stops.
        /// Default value is false.
        /// </summary>
        public bool DeleteCacheAtShutdown { get; set; } = false;

        /// <summary>
        /// The maximum time to wait for a response from a remote connection before throwing a TimeoutException.
        /// </summary>
        public TimeSpan CommunicationsTimeout { get; set; } = TimeSpan.FromSeconds(10);

        /// <summary>
        /// A local file-system path used to cache application assemblies used by the runner.
        /// </summary>
        internal string? AssemblyCachePath { get; set; } = null;

        /// <summary>
        /// A local file-system path used to cache application files requested by a task executing on a runner.
        /// </summary>
        internal string? FileCachePath { get; set; } = null;
    }
}
