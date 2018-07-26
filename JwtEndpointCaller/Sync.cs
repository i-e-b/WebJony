using System;
using System.Threading;
using System.Threading.Tasks;

namespace JwtEndpointCaller
{
    /// <summary>
    /// Helper class to properly wait for async tasks
    /// </summary>
    public static class Sync  
    {
        private static readonly TaskFactory _taskFactory = new
            TaskFactory(CancellationToken.None,
                TaskCreationOptions.None,
                TaskContinuationOptions.None,
                TaskScheduler.Default);

        /// <summary>
        /// Run an async function synchronously and return the result
        /// </summary>
        public static TResult Run<TResult>(Func<Task<TResult>> func) => _taskFactory.StartNew(func).Unwrap().GetAwaiter().GetResult();

        /// <summary>
        /// Run an async function synchronously
        /// </summary>
        public static void Run(Func<Task> func) => _taskFactory.StartNew(func).Unwrap().GetAwaiter().GetResult();
    }
}