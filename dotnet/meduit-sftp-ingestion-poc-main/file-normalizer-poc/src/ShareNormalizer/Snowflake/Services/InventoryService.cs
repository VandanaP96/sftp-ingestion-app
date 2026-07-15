using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Threading;

using Meduit.ShareNormalizer.Snowflake.Constants;
using Meduit.ShareNormalizer.Snowflake.Helpers;
using Meduit.ShareNormalizer.Snowflake.Infrastructure;
using Meduit.ShareNormalizer.Snowflake.Models;
using Meduit.ShareNormalizer.Snowflake.Repository;

namespace Meduit.ShareNormalizer.Snowflake.Services
{
    /// <summary>
    /// Executes the Inventory workflow after normalization.
    ///
    /// Responsibilities
    /// ----------------
    /// 1. Discover normalized folders.
    /// 2. Register Header.
    /// 3. Register Folder.
    /// 4. Validate every file.
    /// 5. Auto reject invalid files.
    /// 6. Move invalid files to Quarantine.
    /// 7. Register every file in Snowflake.
    /// </summary>
    internal sealed class InventoryService
    {

        private readonly object _activityLock = new object();

        private readonly ActivityBuffer _activityBuffer =
    new ActivityBuffer();


        private readonly ConcurrentDictionary<string, bool>
    _duplicateCache =
        new ConcurrentDictionary<string, bool>();

        private readonly Config _config;

        private readonly Logger _logger;

        private readonly SnowflakeContext _context;

        //private readonly SnowCliExecutor _executor;

        private readonly ISnowflakeExecutor _executor;

        private readonly SnowflakeRepositoryContext _repository;

        private readonly FileDiscoveryHelper _discoveryHelper;

        private readonly FileValidator _validator;

        public InventoryService(
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

            _discoveryHelper =
                new FileDiscoveryHelper(config);

            _validator =
                new FileValidator(config);
        }

        /// <summary>
        /// Entry point after normalization.
        /// </summary>
        public void Execute()
        {
            _logger.Log("");
            _logger.Log("=====================================================");
            _logger.Log("STARTING INVENTORY PROCESS");
            _logger.Log("=====================================================");

            List<DiscoveredFolder> folders =
                _discoveryHelper.Discover();

            _logger.Log(
                "Discovered folders : " +
                folders.Count);

            System.Threading.Tasks.Parallel.ForEach(

    folders,

    new System.Threading.Tasks.ParallelOptions
    {
        MaxDegreeOfParallelism =
            _config.InventoryFolderThreads
    },

    folder =>
    {
        try
        {
            ProcessFolder(folder);
        }
        catch (Exception ex)
        {
            _logger.Log("");

            _logger.Log("========================================");

            _logger.Log("FOLDER FAILED");

            _logger.Log("========================================");

            _logger.Log(
                folder.ClientName);

            _logger.Log(
                folder.FolderPath);

            _logger.Log(
                ex.ToString());

            throw;
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

            _logger.Log("");

_logger.Log("======================================================");

_logger.Log("Inventory completed.");

_logger.Log("Folders Processed : " + folders.Count);

_logger.Log("Activities Logged : " +
    _activityBuffer.Count);

_logger.Log("======================================================");
        }

        /// <summary>
        /// Processes one Year-Month folder.
        /// </summary>
        private void ProcessFolder(
    DiscoveredFolder folder)
        {
            InventoryContext context =
                BuildContext(folder);

            List<InventoryWorkItem> folderBuffer =
                new List<InventoryWorkItem>();

            RegisterHeader(context);

            RegisterFolder(context);

            RegisterFiles(
                context,
                folderBuffer);

            FlushDetailBuffer(
                folderBuffer);

            LogFolderSummary(context);
        }

        private InventoryContext BuildContext(
    DiscoveredFolder folder)
{
    InventoryContext context =
        new InventoryContext();

    context.SourceSystem =
        folder.SourceSystem;

    context.ClientName =
        folder.ClientName;

    context.ClientCode =
        folder.ClientName
            .Trim()
            .ToUpperInvariant()
            .Replace(" ", "_");

    context.FolderName =
        folder.FolderName;

    context.FolderPath =
        folder.FolderPath;

    context.ClientRootFolder =
        PathHelper.GetParentFolder(
            folder.FolderPath);

    context.FolderHash =
        FolderHashHelper.Generate(
            folder.FolderPath);

    context.Files =
        folder.Files;

    context.TotalFiles =
        folder.Files.Count;

    context.SuccessfulFiles = 0;

    context.FailedFiles = 0;

    context.QuarantinedFiles = 0;

    context.AutoRejectedFiles = 0;

    context.ApprovedFiles = 0;

    context.UploadedFiles = 0;

    context.ArchivedFiles = 0;

    context.HeaderId = 0;

    context.FolderId = 0;

    context.CurrentUser =
        Environment.UserName;

    context.FolderAlreadyExists = false;

    _logger.Log("");
    _logger.Log("---------------------------------------------------");
    _logger.Log("SYSTEM          : " + context.SourceSystem);
    _logger.Log("CLIENT          : " + context.ClientName);
    _logger.Log("YEAR MONTH      : " + context.FolderName);
    _logger.Log("FILES FOUND     : " + context.TotalFiles);
    _logger.Log("---------------------------------------------------");

    return context;
}

	private void RegisterHeader(
    InventoryContext context)
{
    HeaderRecord header =
        new HeaderRecord();

    header.ClientCode =
        context.ClientCode;

    header.ClientName =
        context.ClientName;

    header.SourceSystem =
        context.SourceSystem;

    header.RootFolder =
        context.ClientRootFolder;

    header.ActiveFlag = "Y";

    header.CreatedBy =
        context.CurrentUser;

    context.HeaderId =
        _repository.Header.GetOrCreate(header);

    if (context.HeaderId <= 0)
        throw new ApplicationException(
            "Unable to retrieve HEADER_ID.");

    ActivityRecord activity =
        new ActivityRecord();

    activity.HeaderId =
        context.HeaderId;

    activity.ActivityType =
        StatusConstants.ActivityType.Header;

    activity.ActivityStatus =
        "SUCCESS";

    activity.ActivityMessage =
        "Header Registered";

    activity.ExecutedBy =
        context.CurrentUser;

    activity.DurationSeconds = 0;

    //_activityRepository.Insert(activity);

    _activityBuffer.Add(activity);

    _logger.Log(
        "HEADER          ID : " +
        context.HeaderId);
}

            private void RegisterFolder(
    InventoryContext context)
{
    FolderRecord folder =
        new FolderRecord();

    folder.HeaderId =
        context.HeaderId;

    folder.YearMonth =
        context.FolderName;

    folder.FolderName =
        context.FolderName;

    folder.FolderPath =
        context.FolderPath;

    folder.FolderHash =
        context.FolderHash;

    folder.FolderStatus =
        StatusConstants.FileStatus.New;

    folder.ActiveFlag = "Y";

    folder.CreatedBy =
        context.CurrentUser;

    context.FolderId =
        _repository.Folder.GetOrCreate(folder);

    if (context.FolderId <= 0)
        throw new ApplicationException(
            "Unable to retrieve Folder ID.");

    ActivityRecord activity =
        new ActivityRecord();

    activity.HeaderId =
        context.HeaderId;

    activity.FolderId =
        context.FolderId;

    activity.ActivityType =
        StatusConstants.ActivityType.Folder;

    activity.ActivityStatus =
        "SUCCESS";

    activity.ActivityMessage =
        "Folder Registered";

    activity.ExecutedBy =
        context.CurrentUser;

    //_activityRepository.Insert(activity);

    _activityBuffer.Add(activity);

    _logger.Log(
        "FOLDER          ID : " +
        context.FolderId);
}

        private void ProcessSingleFile(
            InventoryContext context,
            FileInfo file,
            List<InventoryWorkItem> folderBuffer)
        {
    InventoryWorkItem work =
        new InventoryWorkItem();

    work.File =
        file;

    work.FileHash =
        HashUtil.Sha256File(
            file.FullName);

    work.Validation =
        _validator.Validate(
            file.FullName);

    work.Detail =
        BuildDetailRecord(
            context,
            file,
            work.Validation);

    work.Detail.FileHash =
        work.FileHash;

    bool exists;

    if (!_duplicateCache.TryGetValue(
            work.FileHash,
            out exists))
    {
        exists = false;
    }

    work.AlreadyExists =
        exists;

    if (work.AlreadyExists)
    {
        _logger.Log(
            "Duplicate skipped : "
            + file.Name);

        return;
    }

    if (!work.Validation.IsValid)
    {
        HandleAutoRejectedFile(
            context,
            work);

        return;
    }

            HandleValidFile(
            context,
            work,
            folderBuffer);
        }

        private void RegisterFiles(
     InventoryContext context,
     List<InventoryWorkItem> folderBuffer)
        {
    _logger.Log("");
    _logger.Log("Starting File Inventory...");

    Dictionary<string, bool> duplicateLookup =
        _repository.Detail.ExistsBatch(
            context.FolderId,
            GetHashes(context.Files));

    foreach (KeyValuePair<string, bool> item
        in duplicateLookup)
    {
        _duplicateCache[item.Key] =
            item.Value;
    }

    Parallel.ForEach<FileInfo>(
        context.Files,
        new ParallelOptions
        {
            MaxDegreeOfParallelism =
                Math.Max(
                    2,
                    _config.InventoryThreads)
        },
        file =>
        {
            try
            {
                ProcessSingleFile(
    context,
    file,
    folderBuffer);
            }
            catch (Exception ex)
            {
                lock (_activityLock)
                {
                    context.FailedFiles++;
                }

                ActivityRecord activity =
                    CreateActivity(
                        context,
                        0,
                        StatusConstants.ActivityType.Error,
                        "FAILED",
                        file.Name,
                        ex.ToString());

                _activityBuffer.Add(activity);

                _logger.Log(
                    "FILE FAILED");

                _logger.Log(file.FullName);

                _logger.Log(ex.ToString());
            }
        });

    FlushDetailBuffer(folderBuffer);
}

        private void FlushDetailBuffer(
            List<InventoryWorkItem> folderBuffer)
        {
            if (folderBuffer == null)
                return;

            if (folderBuffer.Count == 0)
                return;

            List<DetailRecord> details =
                new List<DetailRecord>(folderBuffer.Count);

            foreach (InventoryWorkItem work in folderBuffer)
            {
                details.Add(work.Detail);
            }

            _repository.Detail.InsertBatch(details);

            foreach (InventoryWorkItem item in folderBuffer)
            {
                _duplicateCache[item.FileHash] = true;
            }

            _logger.Log(
    "DETAIL BATCH INSERT SUCCESS : "
    + details.Count
    + " rows.");

            folderBuffer.Clear();
        }


        private List<string> GetHashes(
    List<FileInfo> files)
        {
            ConcurrentBag<string> hashes =
                new ConcurrentBag<string>();

            Parallel.ForEach(
                files,
                file =>
                {
                    hashes.Add(
                        HashUtil.Sha256File(
                            file.FullName));
                });

            return new List<string>(hashes);
        }

        private DetailRecord BuildDetailRecord(
    InventoryContext context,
    FileInfo file,
    ValidationResult validation)
{
    DetailRecord detail =
        new DetailRecord();

    detail.FolderId =
        context.FolderId;

    detail.OriginalFileName =
        file.Name;

    detail.CurrentFileName =
        file.Name;

    detail.FileExtension =
        file.Extension;

    detail.FileType =
        file.Extension
            .TrimStart('.')
            .ToUpperInvariant();

    detail.OriginalPath =
        file.FullName;

    detail.CurrentPath =
        file.FullName;

    detail.QuarantinePath = "";

    detail.StagePath = "";

    detail.ArchivePath = "";

    detail.FileSizeKb =
    Math.Round(
        (decimal)file.Length / 1024M,
        2);

    detail.LastModified =
        file.LastWriteTime;

    detail.FileHash = "";

    detail.DatePattern =
        validation.DatePattern;

    detail.ValidDateFlag =
        validation.IsValid ? "Y" : "N";

    detail.ValidationMessage =
        validation.Message;

    detail.FileStatus =
        StatusConstants.FileStatus.New;

    detail.AutoRejectFlag = "N";

    detail.RenameRequiredFlag = "N";

    detail.RenameStatus =
        StatusConstants.RenameStatus.NotRequired;

    detail.ApprovalStatus =
        StatusConstants.ApprovalStatus.Pending;

    detail.IngestionStatus =
        StatusConstants.IngestionStatus.NotStarted;

    detail.RowCount = 0;

    detail.ErrorMessage = "";

    return detail;
}

        private void HandleValidFile(
            InventoryContext context,
            InventoryWorkItem work,
            List<InventoryWorkItem> folderBuffer)
        {
    work.Detail.FileStatus =
        StatusConstants.FileStatus.New;

    work.Detail.ApprovalStatus =
        StatusConstants.ApprovalStatus.Pending;

    work.Detail.AutoRejectFlag =
        "N";

    work.Detail.RenameRequiredFlag =
        "N";

    work.Detail.RenameStatus =
        StatusConstants.RenameStatus.NotRequired;

            lock (folderBuffer)
            {
                folderBuffer.Add(work);

                _duplicateCache.TryAdd(
    work.FileHash,
    true);
            }

            lock (_activityLock)
    {
        context.SuccessfulFiles++;
    }
}

private string MoveToQuarantine(
    InventoryContext context,
    FileInfo file)
{
    string destination =
        PathHelper.BuildQuarantinePath(
            _config.QuarantineRoot,
            _config.NormalizedRoot,
            file.FullName);

    PathHelper.EnsureParentFolder(
        destination);

    if (File.Exists(destination))
    {
        File.Delete(destination);
    }

    File.Move(
        file.FullName,
        destination);


    return destination;
}

private ActivityRecord CreateActivity(
    InventoryContext context,
    long detailId,
    string activityType,
    string status,
    string message,
    string error)
{
    ActivityRecord activity =
        new ActivityRecord();

    activity.HeaderId =
        context.HeaderId;

    activity.FolderId =
        context.FolderId;

    activity.DetailId =
        detailId;

    activity.ActivityType =
        activityType;

    activity.ActivityStatus =
        status;

    activity.ActivityMessage =
        message;

    activity.ExecutedBy =
        context.CurrentUser;

    activity.DurationSeconds = 0;

    activity.ErrorCode = "";

    activity.ErrorMessage =
        error;

    return activity;
}

			private void HandleAutoRejectedFile(
    InventoryContext context,
    InventoryWorkItem work)
{
    string quarantinePath =
        MoveToQuarantine(
            context,
            work.File);

    work.Detail.CurrentPath =
        quarantinePath;

    work.Detail.QuarantinePath =
        quarantinePath;

    work.Detail.FileStatus =
        StatusConstants.FileStatus.AutoRejected;

    work.Detail.AutoRejectFlag =
        "Y";

    work.Detail.RenameRequiredFlag =
        "Y";

    work.Detail.RenameStatus =
        StatusConstants.RenameStatus.NotRequired;

    work.Detail.ApprovalStatus =
        StatusConstants.ApprovalStatus.RenameRequired;

    work.Detail.ValidationMessage =
        work.Validation.Message;

    _repository.Detail.Insert(
        work.Detail);

    lock (_activityLock)
    {
        context.QuarantinedFiles++;

        context.AutoRejectedFiles++;
    }

    ActivityRecord activity =
        CreateActivity(
            context,
            0,
            StatusConstants.ActivityType.AutoReject,
            "SUCCESS",
            work.File.Name,
            work.Validation.Message);

    _activityBuffer.Add(
        activity);
}



private void LogFolderSummary(
    InventoryContext context)
{
    _logger.Log("");

    _logger.Log("==========================================");

    _logger.Log("Inventory Summary");

    _logger.Log("==========================================");

    _logger.Log(
        "Client              : " +
        context.ClientName);

    _logger.Log(
        "Folder              : " +
        context.FolderName);

    _logger.Log(
        "Total Files         : " +
        context.TotalFiles);

    _logger.Log(
        "Registered          : " +
        context.SuccessfulFiles);

    _logger.Log(
        "Auto Rejected       : " +
        context.AutoRejectedFiles);

    _logger.Log(
        "Quarantined         : " +
        context.QuarantinedFiles);

    _logger.Log(
        "Failed              : " +
        context.FailedFiles);

            _logger.Log(
            "Duplicate Files     : "
            + (_duplicateCache.Count));

            _logger.Log(
                "Activity Buffered   : "
                + _activityBuffer.Count);

            _logger.Log("==========================================");
}
		
	}
	
}