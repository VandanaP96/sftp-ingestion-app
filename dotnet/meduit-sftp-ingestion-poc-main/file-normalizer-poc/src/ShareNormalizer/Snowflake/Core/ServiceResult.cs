using System;

namespace Meduit.ShareNormalizer.Core
{
    /// <summary>
    /// Standard response returned by all services.
    /// </summary>
    public sealed class ServiceResult
    {
        public bool Success { get; }

        public string Message { get; }

        public Exception Exception { get; }

        public TimeSpan Duration { get; }

        public int RecordsProcessed { get; }

        public ServiceResult(
            bool success,
            string message = "",
            Exception exception = null,
            TimeSpan duration = default(TimeSpan),
            int recordsProcessed = 0)
        {
            Success = success;
            Message = message;
            Exception = exception;
            Duration = duration;
            RecordsProcessed = recordsProcessed;
        }

        public static ServiceResult Ok(
            int recordsProcessed = 0,
            TimeSpan duration = default(TimeSpan))
        {
            return new ServiceResult(
                true,
                "",
                null,
                duration,
                recordsProcessed);
        }

        public static ServiceResult Fail(
            string message,
            Exception exception = null)
        {
            return new ServiceResult(
                false,
                message,
                exception);
        }
    }
}