using System.Threading;

namespace Microsoft.Owin.Throttling.Implementation
{
    public class DefaultThreadingServices : IThreadingServices
    {
        public ThreadCounts GetAvailableThreads()
        {
            int workerThreads;
            int completionPortThreads;
            ThreadPool.GetAvailableThreads(out workerThreads, out completionPortThreads);
            return new ThreadCounts
            {
                WorkerThreads = workerThreads,
                CompletionPortThreads = completionPortThreads
            };
        }

        public ThreadCounts GetMaxThreads()
        {
            int workerThreads;
            int completionPortThreads;
            ThreadPool.GetMaxThreads(out workerThreads, out completionPortThreads);
            return new ThreadCounts
            {
                WorkerThreads = workerThreads,
                CompletionPortThreads = completionPortThreads
            };
        }
    }
}