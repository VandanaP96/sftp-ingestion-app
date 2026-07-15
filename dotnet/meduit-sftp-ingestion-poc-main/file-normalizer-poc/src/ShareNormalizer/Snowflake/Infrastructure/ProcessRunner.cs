using System;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace Meduit.ShareNormalizer.Snowflake.Infrastructure
{
    /// <summary>
    /// Executes external processes (SnowCLI) and captures
    /// ExitCode, Standard Output, Standard Error and Duration.
    /// Thread-safe with configurable concurrent process limit.
    /// </summary>
    internal sealed class ProcessRunner
    {
        internal sealed class ProcessResult
        {
            public int ExitCode;

            public string StandardOutput;

            public string StandardError;

            public TimeSpan Duration;

            public bool Success
            {
                get
                {
                    return ExitCode == 0;
                }
            }
        }

        private readonly Logger _logger;

        private static SemaphoreSlim _processLimiter;

        private static readonly object _lock =
            new object();

        public ProcessRunner(
            Config config,
            Logger logger)
        {
            _logger = logger;

            if (_processLimiter == null)
            {
                lock (_lock)
                {
                    if (_processLimiter == null)
                    {
                        _processLimiter =
                            new SemaphoreSlim(
                                config.SnowCliThreads,
                                config.SnowCliThreads);
                    }
                }
            }
        }

        /// <summary>
        /// Executes SnowCLI process.
        /// </summary>
        public ProcessResult Execute(
            string executable,
            string arguments)
        {
            _processLimiter.Wait();

            Process process = null;

            try
            {
                ProcessResult result =
                    new ProcessResult();

                Stopwatch watch =
                    Stopwatch.StartNew();

                StringBuilder stdout =
                    new StringBuilder();

                StringBuilder stderr =
                    new StringBuilder();

                process =
                    new Process();

                process.StartInfo.FileName =
                    executable;

                process.StartInfo.Arguments =
                    arguments;

                process.StartInfo.CreateNoWindow =
                    true;

                process.StartInfo.UseShellExecute =
                    false;

                process.StartInfo.RedirectStandardOutput =
                    true;

                process.StartInfo.RedirectStandardError =
                    true;

                process.OutputDataReceived +=
                    (s, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                        {
                            stdout.AppendLine(e.Data);
                        }
                    };

                process.ErrorDataReceived +=
                    (s, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                        {
                            stderr.AppendLine(e.Data);
                        }
                    };

                _logger.Log(
    "SNOWCLI EXECUTE");

                process.Start();

                process.BeginOutputReadLine();

                process.BeginErrorReadLine();

                process.WaitForExit();

                watch.Stop();

                result.ExitCode =
                    process.ExitCode;

                result.StandardOutput =
                    stdout.ToString();

                result.StandardError =
                    stderr.ToString();

                result.Duration =
                    watch.Elapsed;

                if (result.Success)
                {
                    // Remove this success log.
                    // We'll log service summaries instead.
                }
                else
                {
                    _logger.Log(
                        string.Format(
                            "PROCESS     FAILED ({0})",
                            result.ExitCode));

                    if (!string.IsNullOrWhiteSpace(
                            result.StandardError))
                    {
                        _logger.Log(
                            result.StandardError);
                    }
                }

                return result;
            }
            finally
            {
                if (process != null)
                {
                    process.Dispose();
                }

                _processLimiter.Release();
            }
        }
    }
}