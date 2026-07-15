using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

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

        private readonly ISnowflakeExecutor _sqlExecutor;

        private readonly ISnowflakeExecutor _stageExecutor;

        private readonly DetailRepository _detailRepository;

        private readonly ActivityRepository _activityRepository;

        private readonly object _activityLock =
            new object();

        private readonly object _databaseLock =
        new object();

        private readonly ActivityBuffer _activityBuffer =
            new ActivityBuffer();

        private readonly object _statisticsLock =
            new object();

        private int _uploadedFiles;

        private int _failedFiles;

        private int _skippedFiles;

        private DateTime _started;

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
                new ProcessRunner(
                    config,
                    logger);

            SnowflakeExecutorFactory factory =
                new SnowflakeExecutorFactory(
                    _context,
                    runner,
                    logger);

            _sqlExecutor =
                factory.SqlExecutor;

            _stageExecutor =
                factory.StageExecutor;

            _detailRepository =
                new DetailRepository(
                    _context,
                    _sqlExecutor,
                    logger);

            _activityRepository =
                new ActivityRepository(
                    _context,
                    _sqlExecutor,
                    logger);
        }

        /// <summary>
        /// Entry point.
        /// </summary>
        public void Execute()
        {
            LogServiceStart();

            _started = DateTime.Now;

            List<StageUploadJob> jobs =
                _detailRepository.GetStageUploadJobs();

            _logger.Log(
                "Pending Upload Jobs : "
                + jobs.Count);

            Parallel.ForEach(
                jobs,
                new ParallelOptions
                {
                    MaxDegreeOfParallelism =
                        _config.StageUploadThreads
                },
                job =>
                {
                    try
                    {
                        ProcessUpload(job);
                    }
                    catch (Exception ex)
                    {
                        LogFailure(job, ex);

                        _detailRepository.UpdateError(
                            job.DetailId,
                            ex.Message);

                        lock (_activityLock)
                        {
                            _failedFiles++;

                            _activityBuffer.Add(
                                CreateActivity(
                                    job.DetailId,
                                    "UPLOAD",
                                    "FAILED",
                                    ex.Message));
                        }
                    }
                });

            List<ActivityRecord> activities =
                _activityBuffer.Drain();

            if (activities.Count > 0)
            {
                _activityRepository
                    .InsertBatchTransaction(
                        activities);
            }

            LogServiceCompleted();
        }

        private void ProcessUpload(
    StageUploadJob job)
        {
            ValidateJob(job);

            LogUploadJob(job);

            _detailRepository.UpdateIngestionStart(
    job.DetailId);

            UploadToStage(job);

            string archiveFile =
                MoveToArchive(job);

            lock (_databaseLock)
            {
                _sqlExecutor.BeginTransaction();

                try
                {
                    UpdateDatabase(
                        job,
                        archiveFile);

                    _activityBuffer.Add(
                        CreateActivity(
                            job.DetailId,
                            "UPLOAD",
                            "SUCCESS",
                            "Upload completed"));

                    _sqlExecutor.CommitTransaction();
                }
                catch
                {
                    _sqlExecutor.RollbackTransaction();
                    throw;
                }
            }

            System.Threading.Interlocked.Increment(
                ref _uploadedFiles);

            LogSuccess(job);
        }

        private bool StageAlreadyContainsFile(
    StageUploadJob job)
        {
            // No longer used.
            return false;
        }


        /// <summary>
        /// Determines whether the file already exists
        /// in the configured Snowflake stage.
        /// </summary>
        //    private bool StageAlreadyContainsFile(
        //StageUploadJob job)
        //    {
        //        string stageFolder =
        //            BuildStageFolder(job);

        //        return
        //            _stageExecutor.StageFileExists(
        //                stageFolder,
        //                job.CurrentFileName);
        //    }



        /// <summary>
        /// Uploads one file into Snowflake stage.
        /// </summary>
        private void UploadToStage(
    StageUploadJob job)
        {
            const int maxRetry = 3;

            Exception lastException = null;

            for (int attempt = 1;
                 attempt <= maxRetry;
                 attempt++)
            {
                try
                {
                    _logger.Log(
                        "Uploading to Snowflake Stage... Attempt "
                        + attempt);

                    string stageFolder =
                        BuildStageFolder(job);

                    bool uploaded =
                        _stageExecutor.PutFile(
                            job.CurrentPath,
                            stageFolder);

                    if (!uploaded)
                    {
                        throw new ApplicationException(
                            "PUT returned FALSE.");
                    }

                    return;
                }
                catch (Exception ex)
                {
                    lastException = ex;

                    _logger.Log(
                        "PUT failed. Retry "
                        + attempt);

                    if (attempt < maxRetry)
                    {
                        System.Threading.Thread.Sleep(2000);
                    }
                }
            }

            throw lastException;
        }

        /// <summary>
        /// Verifies that the uploaded file exists
        /// inside the Snowflake stage.
        /// </summary>
        private void VerifyUpload(
    StageUploadJob job)
        {
            // Disabled.
            // SnowCLI 3.x no longer supports the old
            // stage list implementation used here.
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

        if (!File.Exists(archiveFile))
{
    throw new ApplicationException(
        "Archive verification failed.");
}

        _logger.Log(
    "Archive completed.");

_logger.Log(
    archiveFile);    

            return archiveFile;
        }

        /// <summary>
        /// Builds the Snowflake stage folder.
        /// </summary>
        private string BuildStageFolder(
            StageUploadJob job)
        {
            

            string normalizedRoot =
    Path.GetFullPath(
        _config.NormalizedRoot);

string currentFolder =
    Path.GetFullPath(
        Path.GetDirectoryName(
            job.CurrentPath));

string relativePath =
    currentFolder.Substring(
        normalizedRoot.Length);

relativePath =
    relativePath
        .TrimStart('\\')
        .Replace("\\", "/");

            _logger.Log(
    "Stage Folder = " + relativePath);
    
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
                "Updating database...");

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
                "Database updated.");
        }

        /// <summary>
        /// Returns the full stage path stored in DB.
        /// </summary>
        private string BuildStageFullPath(
            StageUploadJob job)
        {
            string folder =
                BuildStageFolder(job);

            string fullPath =
    string.Format(
                "@{0}/{1}/{2}",
                _config.SnowflakeStage,
                folder.Replace("\\", "/"),
                job.CurrentFileName);

                _logger.Log(
    "Stage Full Path : "
    + fullPath);

return fullPath;

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
    _logger.Log("");

    _logger.Log(
        "======================================");

    _logger.Log(
        "UPLOAD SUCCESS");

    _logger.Log(
        "DETAIL ID : "
        + job.DetailId);

    _logger.Log(
        "FILE      : "
        + job.CurrentFileName);

    _logger.Log(
        "ARCHIVED");

    _logger.Log(
        "======================================");
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
        "UPLOAD FAILED");

    _logger.Log(
        "DETAIL ID : " +
        job.DetailId);

    _logger.Log(
        "FILE      : " +
        job.CurrentFileName);

    _logger.Log(
        "ERROR     : " +
        ex.Message);

    if (ex.InnerException != null)
    {
        _logger.Log(
            ex.InnerException.Message);
    }

    _logger.Log(
        "======================================");
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

            if (new FileInfo(job.CurrentPath).Length == 0)
{
    throw new ApplicationException(
        "Cannot upload an empty file.");
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

        private void LogStatistics()
{
    _logger.Log("");

    _logger.Log(
        "Upload Statistics");

    _logger.Log(
        "Uploaded : "
        + _uploadedFiles);

    _logger.Log(
        "Skipped  : "
        + _skippedFiles);

    _logger.Log(
        "Failed   : "
        + _failedFiles);
}

        /// <summary>
        /// Service completed.
        /// </summary>
        private void LogServiceCompleted()
{
    TimeSpan elapsed =
        DateTime.Now - _started;

    _logger.Log("");

    _logger.Log("======================================");

    _logger.Log("Stage Upload Service Completed");

    _logger.Log("Uploaded Files : " +
        _uploadedFiles);

    _logger.Log("Failed Files   : " +
        _failedFiles);

    _logger.Log("Duration       : " +
        elapsed);

    _logger.Log("======================================");
}
    }
}

