using System;
using System.Collections.Generic;
using System.IO;

using Meduit.ShareNormalizer.Snowflake.Helpers;
using Meduit.ShareNormalizer.Snowflake.Infrastructure;
using Meduit.ShareNormalizer.Snowflake.Models;
using Meduit.ShareNormalizer.Snowflake.Repository;

namespace Meduit.ShareNormalizer.Snowflake.Services
{
    /// <summary>
    /// Uploads approved files into Snowflake stage,
    /// archives them and updates metadata.
    /// </summary>
    internal sealed class StageUploadService
    {
        private readonly Config _config;

        private readonly Logger _logger;

        private readonly SnowflakeContext _context;

        private readonly SnowCliExecutor _executor;

        private readonly DetailRepository _detailRepository;

        private readonly ActivityRepository _activityRepository;

        public StageUploadService(
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
                new ProcessRunner(logger);

            _executor =
                new SnowCliExecutor(
                    _context,
                    runner,
                    logger);

            _detailRepository =
                new DetailRepository(
                    _context,
                    _executor,
                    logger);

            _activityRepository =
                new ActivityRepository(
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

            List<StageUploadJob> jobs =
                _detailRepository.GetStageUploadJobs();

            _logger.Log(
                string.Format(
                    "Pending Upload Jobs : {0}",
                    jobs.Count));

            foreach (StageUploadJob job in jobs)
            {
                try
                {
                    ProcessUpload(job);
                }
                catch (Exception ex)
{
    LogFailure(job, ex);

    ActivityRecord activity =
        CreateActivity(
            job.DetailId,
            "UPLOAD",
            "FAILED",
            ex.Message);

    _activityRepository.Insert(
        activity);
}
            }

            LogServiceCompleted();
        }

                private void ProcessUpload(
    StageUploadJob job)
{
    ValidateJob(job);

    LogUploadJob(job);

    _logger.Log("");

    _logger.Log(
        "Uploading : " +
        job.CurrentFileName);

    if (StageAlreadyContainsFile(job))
{
    _logger.Log(
        "File already exists in Snowflake stage.");

    VerifyUpload(job);
}
else
{
    UploadToStage(job);

    VerifyUpload(job);
}

    string archiveFile =
        MoveToArchive(job);

    UpdateDatabase(
        job,
        archiveFile);

    ActivityRecord activity =
        CreateActivity(
            job.DetailId,
            "UPLOAD",
            "SUCCESS",
            "Stage upload completed.");

    _activityRepository.Insert(
        activity);

    LogSuccess(job);
}


/// <summary>
/// Determines whether the file already exists
/// in the configured Snowflake stage.
/// </summary>
private bool StageAlreadyContainsFile(
    StageUploadJob job)
{
    string stageFolder =
        BuildStageFolder(job);

    return
        _executor.StageFileExists(
            stageFolder,
            job.CurrentFileName);
}


                /// <summary>
        /// Uploads one file into Snowflake stage.
        /// </summary>
        private void UploadToStage(
            StageUploadJob job)
        {
            _logger.Log(
                "Uploading to Snowflake stage...");

            string stageFolder =
                BuildStageFolder(job);

            bool uploaded =
                _executor.PutFile(
                    job.CurrentPath,
                    stageFolder);

            if (!uploaded)
            {
                throw new ApplicationException(
                    "Snowflake PUT command failed.");
            }

            _logger.Log(
                "PUT completed successfully.");
        }

        /// <summary>
        /// Verifies that the uploaded file exists
        /// inside the Snowflake stage.
        /// </summary>
        private void VerifyUpload(
            StageUploadJob job)
        {
            _logger.Log(
                "Verifying stage upload...");

            string stageFolder =
                BuildStageFolder(job);

            bool exists =
                _executor.StageFileExists(
                    stageFolder,
                    job.CurrentFileName);

            _logger.Log(
                         "Checking stage folder : " +
                          stageFolder);

            if (!exists)
            {
                throw new ApplicationException(
                    "Uploaded file not found inside Snowflake stage.");
            }

            _logger.Log(
                "Stage verification successful.");
        }

        /// <summary>
        /// Moves uploaded file into archive.
        /// </summary>
        private string MoveToArchive(
            StageUploadJob job)
        {
            _logger.Log(
                "Moving file to archive...");

            string archiveFolder =
                BuildArchiveFolder(job);

            string archiveFile =
                Path.Combine(
                    archiveFolder,
                    job.CurrentFileName);

            if (File.Exists(archiveFile))
{
    File.Delete(
        archiveFile);
}

archiveFile =
    FileMovementHelper.MoveFile(
        job.CurrentPath,
        archiveFile);

            _logger.Log(
                "Archived : " +
                archiveFile);

            return archiveFile;
        }

        /// <summary>
        /// Builds the Snowflake stage folder.
        /// </summary>
        private string BuildStageFolder(
            StageUploadJob job)
        {
            string relativePath =
                Path.GetDirectoryName(
                    job.CurrentPath);

            relativePath =
                relativePath.Replace(
                    _config.NormalizedRoot,
                    "");

            relativePath =
                relativePath
                    .TrimStart('\\')
                    .Replace("\\", "/");

            return relativePath;
        }

        /// <summary>
        /// Builds archive folder preserving
        /// normalized folder hierarchy.
        /// </summary>
        private string BuildArchiveFolder(
            StageUploadJob job)
        {
            string relativePath =
                Path.GetDirectoryName(
                    job.CurrentPath);

            relativePath =
                relativePath.Replace(
                    _config.NormalizedRoot,
                    "");

            relativePath =
                relativePath.TrimStart('\\');

            return Path.Combine(
                _config.ArchiveRoot,
                relativePath);
        }


                /// <summary>
        /// Updates metadata after successful upload
        /// and archive.
        /// </summary>
        private void UpdateDatabase(
            StageUploadJob job,
            string archiveFile)
        {
            _logger.Log(
                "Updating upload metadata...");

            bool updated =
                _detailRepository.FinishUpload(
                    job.DetailId,
                    BuildStageFullPath(job),
                    archiveFile);

            if (!updated)
            {
                throw new ApplicationException(
                    "Unable to update upload metadata.");
            }

            _logger.Log(
                "Metadata updated successfully.");
        }

        /// <summary>
        /// Returns the full stage path stored in DB.
        /// </summary>
        private string BuildStageFullPath(
            StageUploadJob job)
        {
            string folder =
                BuildStageFolder(job);

            return string.Format(
                "@{0}/{1}/{2}",
                _config.SnowflakeStage,
                folder.Replace("\\", "/"),
                job.CurrentFileName);
        }

        /// <summary>
        /// Creates upload activity.
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
        /// Logs upload job.
        /// </summary>
        private void LogUploadJob(
            StageUploadJob job)
        {
            _logger.Log("");

            _logger.Log(
                "======================================");

            _logger.Log(
                "DETAIL_ID      : " +
                job.DetailId);

            _logger.Log(
                "FILE           : " +
                job.CurrentFileName);

            _logger.Log(
                "CURRENT PATH   : " +
                job.CurrentPath);

            _logger.Log(
                "STAGE PATH     : " +
                BuildStageFullPath(job));

            _logger.Log(
                "======================================");
        }

        /// <summary>
        /// Logs upload success.
        /// </summary>
        private void LogSuccess(
            StageUploadJob job)
        {
            _logger.Log(
                "Upload completed : " +
                job.CurrentFileName);
        }

        /// <summary>
        /// Logs upload failure.
        /// </summary>
        private void LogFailure(
            StageUploadJob job,
            Exception ex)
        {
            _logger.Log("");

            _logger.Log(
                "Upload failed.");

            _logger.Log(
                "DETAIL_ID : " +
                job.DetailId);

            _logger.Log(
                ex.Message);
        }

        /// <summary>
        /// Validates upload job.
        /// </summary>
        private void ValidateJob(
            StageUploadJob job)
        {
            if (job == null)
            {
                throw new ArgumentNullException(
                    "job");
            }

            if (string.IsNullOrWhiteSpace(
                job.CurrentPath))
            {
                throw new ApplicationException(
                    "Current path is empty.");
            }

            if (!File.Exists(
                job.CurrentPath))
            {
                throw new FileNotFoundException(
                    "Upload file not found.",
                    job.CurrentPath);
            }

            if (string.IsNullOrWhiteSpace(
                job.CurrentFileName))
            {
                throw new ApplicationException(
                    "Current file name is empty.");
            }
        }

        /// <summary>
        /// Service started.
        /// </summary>
        private void LogServiceStart()
        {
            _logger.Log("");

            _logger.Log(
                "======================================");

            _logger.Log(
                "Stage Upload Service Started");

            _logger.Log(
                "======================================");
        }

        /// <summary>
        /// Service completed.
        /// </summary>
        private void LogServiceCompleted()
        {
            _logger.Log("");

            _logger.Log(
                "======================================");

            _logger.Log(
                "Stage Upload Service Completed");

            _logger.Log(
                "======================================");
        }
    }
}

