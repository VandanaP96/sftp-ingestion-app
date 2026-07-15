using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Meduit.ShareNormalizer.Snowflake.Helpers;
using Meduit.ShareNormalizer.Snowflake.Infrastructure;
using Meduit.ShareNormalizer.Snowflake.Models;
using Meduit.ShareNormalizer.Snowflake.Repository;

namespace Meduit.ShareNormalizer.Snowflake.Services
{
    /// <summary>
    /// Processes files that were automatically quarantined,
    /// renamed by SME and approved in Streamlit.
    ///
    /// Workflow
    ///
    /// Read READY rename jobs
    ///          │
    ///          ▼
    /// Validate record
    ///          │
    ///          ▼
    /// Move + Rename
    /// (Quarantine → Normalized)
    ///          │
    ///          ▼
    /// Update metadata
    ///          │
    ///          ▼
    /// Insert Activity
    /// </summary>
    internal sealed class RenameService
    {
        private readonly Config _config;

        private readonly Logger _logger;

        private readonly SnowflakeContext _context;

        //private readonly SnowCliExecutor _executor;

        private readonly ISnowflakeExecutor _executor;

        private readonly SnowflakeRepositoryContext
    _repository;

        //private readonly object _fileMoveLock = new object();

    private readonly ActivityBuffer _activityBuffer =
    new ActivityBuffer();

        private readonly object _activityLock =
    new object();

        public RenameService(
            Config config,
            Logger logger)
        {
            _config = config;

            _logger = logger;

            _context =
                new SnowflakeContext(
                    config,
                    logger);

            ProcessRunner runner =
    new ProcessRunner(
        config,
        logger);

SnowflakeExecutorFactory factory =
    new SnowflakeExecutorFactory(
        _context,
        runner,
        logger);

_executor =
    factory.SqlExecutor;

            _repository =
    new SnowflakeRepositoryContext(
        _context,
        _executor,
        logger);
        
        }

        /// <summary>
        /// Entry point.
        /// </summary>
        public void Execute()
        {
            LogServiceStart();

            List<RenameJob> jobs =
                _repository.Detail.GetRenameJobs();

            _logger.Log(
                "Rename Jobs Returned : "
                + jobs.Count);

            _logger.Log(
                string.Format(
                    "Rename jobs found : {0}",
                    jobs.Count));

            Parallel.ForEach(

    jobs,

    new ParallelOptions
    {
        MaxDegreeOfParallelism =
            _config.RenameThreads
    },

    job =>
    {
        try
        {
            ProcessRenameJob(job);
        }
        catch (Exception ex)
        {
            _logger.Log(
                "RENAME ERROR : " +
                ex.Message);

            ActivityRecord activity =
    CreateActivity(
        job.DetailId,
        "RENAME",
        "FAILED",
        ex.Message);

_activityBuffer.Add(activity);
        }
    });

            List<ActivityRecord> activities =
    _activityBuffer.Drain();

if (activities.Count > 0)
{
    _repository.Activity
        .InsertBatchTransaction(
            activities);
}

LogServiceCompleted();
        }

        /// <summary>
        /// Processes one rename request.
        /// </summary>
        private void ProcessRenameJob(
            RenameJob job)
        {
            ValidateJob(job);

            if (!IsJobValid(job))
{
    throw new ApplicationException(
        "Invalid rename job.");
}

EnsureDestinationFolder(job);

            LogRenameJob(job);

            string normalizedFile =
                MoveAndRename(job);

            UpdateDatabase(
                job,
                normalizedFile);

            ActivityRecord activity =
    CreateActivity(
        job.DetailId,
        "RENAME",
        "SUCCESS",
        "Rename completed successfully.");

_activityBuffer.Add(activity);

        }

        /// <summary>
        /// Validates rename request.
        /// </summary>
        private void ValidateJob(
            RenameJob job)
        {
            if (job == null)
            {
                throw new ArgumentNullException(
                    "job");
            }

            if (string.IsNullOrWhiteSpace(
                job.QuarantinePath))
            {
                throw new ApplicationException(
                    "Quarantine path is empty.");
            }

            if (!File.Exists(
                job.QuarantinePath))
            {
                throw new FileNotFoundException(
                    "Quarantined file not found.",
                    job.QuarantinePath);
            }

            if (string.IsNullOrWhiteSpace(
                job.CurrentFileName))
            {
                throw new ApplicationException(
                    "Current file name is empty.");
            }

            if (string.IsNullOrWhiteSpace(
                job.OriginalPath))
            {
                throw new ApplicationException(
                    "Original path is empty.");
            }
        }

                /// <summary>
        /// Moves the file from Quarantine back to the
        /// Normalized folder while renaming it.
        /// </summary>
        private string MoveAndRename(
            RenameJob job)
        {
            _logger.Log("");

            _logger.Log(
                "Moving file from Quarantine to Normalized...");

            string normalizedFolder =
                Path.GetDirectoryName(
                    job.OriginalPath);

            if (string.IsNullOrWhiteSpace(
                normalizedFolder))
            {
                throw new ApplicationException(
                    "Unable to determine normalized folder.");
            }

            string destinationFile =
                Path.Combine(
                    normalizedFolder,
                    job.CurrentFileName);

            _logger.Log(
                "SOURCE      : " +
                job.QuarantinePath);

            _logger.Log(
                "DESTINATION : " +
                destinationFile);

            destinationFile =
    FileMovementHelper.MoveFile(
        job.QuarantinePath,
        destinationFile);

            _logger.Log(
                "File moved successfully.");

            return destinationFile;
        }

        /// <summary>
        /// Updates metadata after successful rename.
        /// </summary>
        private void UpdateDatabase(
            RenameJob job,
            string normalizedFile)
        {
            _logger.Log(
                "Updating metadata...");

            bool updated;

updated =
    _repository.Detail.FinishRename(
        job.DetailId,
        job.CurrentFileName,
        normalizedFile);

            if (!updated)
            {
                throw new ApplicationException(
                    "Unable to update FILE_BATCH_DETAIL.");
            }

            _logger.Log(
                "Metadata updated successfully.");
        }

        /// <summary>
        /// Creates an activity record.
        /// </summary>
        private ActivityRecord CreateActivity(
            long detailId,
            string activityType,
            string status,
            string message)
        {
            ActivityRecord activity =
                new ActivityRecord();

            activity.DetailId =
                detailId;

            activity.ActivityType =
                activityType;

            activity.ActivityStatus =
                status;

            activity.ActivityMessage =
                message;

            activity.ExecutedBy =
                Environment.UserName;

            activity.DurationSeconds =
                0;

            activity.ErrorCode =
                "";

            activity.ErrorMessage =
                "";

            return activity;
        }

        /// <summary>
        /// Writes job information into the log.
        /// </summary>
        private void LogRenameJob(
            RenameJob job)
        {
            _logger.Log("");

            _logger.Log(
                "========================================");

            _logger.Log(
                "DETAIL_ID      : " +
                job.DetailId);

            _logger.Log(
                "ORIGINAL NAME  : " +
                job.OriginalFileName);

            _logger.Log(
                "CURRENT NAME   : " +
                job.CurrentFileName);

            _logger.Log(
                "QUARANTINE     : " +
                job.QuarantinePath);

            _logger.Log(
                "ORIGINAL PATH  : " +
                job.OriginalPath);

            _logger.Log(
                "========================================");
        }

        

                /// <summary>
        /// Determines whether the rename job is still valid.
        /// This prevents processing stale records.
        /// </summary>
        private bool IsJobValid(
            RenameJob job)
        {
            if (job == null)
                return false;

            if (job.DetailId <= 0)
                return false;

            if (string.IsNullOrWhiteSpace(
                job.CurrentFileName))
                return false;

            if (string.IsNullOrWhiteSpace(
                job.QuarantinePath))
                return false;

            return true;
        }

        /// <summary>
        /// Ensures the normalized destination folder exists.
        /// </summary>
        private void EnsureDestinationFolder(
            RenameJob job)
        {
            string folder =
                Path.GetDirectoryName(
                    job.OriginalPath);

            if (string.IsNullOrWhiteSpace(folder))
            {
                throw new ApplicationException(
                    "Unable to determine destination folder.");
            }

            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }
        }

        /// <summary>
        /// Writes start banner.
        /// </summary>
        private void LogServiceStart()
        {
            _logger.Log("");
            _logger.Log("==========================================");
            _logger.Log("Rename Service Started");
            _logger.Log("==========================================");
        }

        /// <summary>
        /// Writes completion banner.
        /// </summary>
        private void LogServiceCompleted()
        {
            _logger.Log("");
            _logger.Log("==========================================");
            _logger.Log("Rename Service Completed");
            _logger.Log("==========================================");
        }
    }
}