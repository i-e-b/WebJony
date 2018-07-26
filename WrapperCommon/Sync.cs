using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace WrapperCommon
{
    /// <summary>
    /// Helper class to properly wait for async tasks
    /// </summary>
    public static class Sync  
    {
        [ThreadStatic]
        private static TaskFactory _taskFactory;

        /// <summary>
        /// Run an async function synchronously and return the result
        /// </summary>
        public static TResult Run<TResult>([JetBrains.Annotations.InstantHandle]Func<Task<TResult>> func)
        {
            EnsureFactory();
            try
            {
                return _taskFactory.StartNew(func).Unwrap().ConfigureAwait(continueOnCapturedContext: false).GetAwaiter().GetResult();
            }
            catch (TaskCanceledException tcex)
            {
                Trace.TraceError("Task exception: " + tcex);
                return default(TResult);
            }
        }

        /// <summary>
        /// Run an async function synchronously
        /// </summary>
        public static void Run([JetBrains.Annotations.InstantHandle]Func<Task> func) {
            EnsureFactory();
            _taskFactory.StartNew(func).Unwrap().ConfigureAwait(continueOnCapturedContext: false).GetAwaiter().GetResult();
        }

        private static void EnsureFactory()
        {
            if (_taskFactory == null)
            {
                _taskFactory = new
                    TaskFactory(CancellationToken.None,
                        TaskCreationOptions.None,
                        TaskContinuationOptions.ExecuteSynchronously,
                        TaskScheduler.Current);
            }
        }

    }
}
namespace JetBrains.Annotations{
    [AttributeUsage(AttributeTargets.All)]
    public class InstantHandleAttribute : Attribute { }
}