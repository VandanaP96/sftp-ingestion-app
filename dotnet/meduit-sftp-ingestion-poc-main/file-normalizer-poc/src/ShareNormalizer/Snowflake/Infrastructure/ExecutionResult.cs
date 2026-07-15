using System;

namespace Meduit.ShareNormalizer.Snowflake.Infrastructure
{
    internal sealed class ExecutionResult
    {
        public bool Success { get; set; }

        public string Output { get; set; }

        public string Error { get; set; }

        public Exception Exception { get; set; }

        public TimeSpan Duration { get; set; }

        public int RowsAffected { get; set; }

        public string ExecutorName { get; set; }

        public bool IsRetry { get; set; }

        public int RetryAttempt { get; set; }

        public static ExecutionResult Ok()
        {
            return new ExecutionResult
            {
                Success = true
            };
        }

        public static ExecutionResult Fail(
            string error)
        {
            return new ExecutionResult
            {
                Success = false,
                Error = error
            };
        }
    }
}