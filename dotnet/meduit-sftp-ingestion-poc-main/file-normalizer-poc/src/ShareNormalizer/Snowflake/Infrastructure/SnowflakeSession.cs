using System;

namespace Meduit.ShareNormalizer.Snowflake.Infrastructure
{
    /// <summary>
    /// Represents one Snowflake execution session.
    /// The session owns exactly one executor instance.
    /// </summary>
    internal sealed class SnowflakeSession
        : IDisposable
    {
        private readonly ISnowflakeExecutor _executor;

        public ISnowflakeExecutor Executor
        {
            get { return _executor; }
        }

        public SnowflakeSession(
            ISnowflakeExecutor executor)
        {
            if (executor == null)
                throw new ArgumentNullException("executor");

            _executor = executor;
        }

        public bool Initialize()
{
    return _executor.TestConnection();
}  

        public void Dispose()
        {
            IDisposable disposable =
                _executor as IDisposable;

            if (disposable != null)
            {
                disposable.Dispose();
            }
        }
    }
}