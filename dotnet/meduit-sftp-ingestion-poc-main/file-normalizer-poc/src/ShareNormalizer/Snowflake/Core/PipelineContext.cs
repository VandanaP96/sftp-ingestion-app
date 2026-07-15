using System;

namespace Meduit.ShareNormalizer.Core
{
    /// <summary>
    /// Shared execution context throughout one pipeline execution.
    /// </summary>
    public sealed class PipelineContext
    {
        public Guid RunId { get; }

        public string ClientName { get; set; }

        public DateTime StartTime { get; }

        public PipelineContext()
        {
            RunId = Guid.NewGuid();
            StartTime = DateTime.Now;
        }
    }
}