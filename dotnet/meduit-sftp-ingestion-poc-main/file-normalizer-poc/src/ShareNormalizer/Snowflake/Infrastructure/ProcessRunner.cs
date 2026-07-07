using System;
using System.Diagnostics;
using System.Text;

namespace Meduit.ShareNormalizer.Snowflake.Infrastructure
{
    /// <summary>
    /// Executes external processes (SnowCLI) and captures
    /// ExitCode, Standard Output, Standard Error and Duration.
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
                get { return ExitCode == 0; }
            }
        }

        private readonly Logger _logger;

        public ProcessRunner(Logger logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Execute an executable with arguments.
        /// </summary>
        public ProcessResult Execute(string executable, string arguments)
        {
            var result = new ProcessResult();

            var watch = Stopwatch.StartNew();

            var stdout = new StringBuilder();
            var stderr = new StringBuilder();

            var process = new Process();

            process.StartInfo.FileName = executable;
            process.StartInfo.Arguments = arguments;

            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;

            process.OutputDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    stdout.AppendLine(e.Data);
            };

            process.ErrorDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    stderr.AppendLine(e.Data);
            };

            _logger.Log("PROCESS     " + executable + " " + arguments);

            process.Start();

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            process.WaitForExit();

            watch.Stop();

            result.ExitCode = process.ExitCode;
            result.StandardOutput = stdout.ToString();
            result.StandardError = stderr.ToString();
            result.Duration = watch.Elapsed;

            if (result.Success)
            {
                _logger.Log(
                    string.Format(
                        "PROCESS     SUCCESS ({0} ms)",
                        result.Duration.TotalMilliseconds));
            }
            else
            {
                _logger.Log(
                    string.Format(
                        "PROCESS     FAILED ({0})",
                        result.ExitCode));

                if (!string.IsNullOrWhiteSpace(result.StandardError))
                    _logger.Log(result.StandardError);
            }

            process.Dispose();

            return result;
        }
    }
}