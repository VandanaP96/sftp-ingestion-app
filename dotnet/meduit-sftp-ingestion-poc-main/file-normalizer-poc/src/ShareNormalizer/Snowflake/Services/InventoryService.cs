using System;
using System.Collections.Generic;
using System.IO;

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
        private readonly Config _config;

        private readonly Logger _logger;

        private readonly SnowflakeContext _context;

        private readonly SnowCliExecutor _executor;

        private readonly HeaderRepository _headerRepository;

        private readonly FolderRepository _folderRepository;

        private readonly DetailRepository _detailRepository;

        private readonly ActivityRepository _activityRepository;

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
    new ProcessRunner(logger);

            _executor =
                new SnowCliExecutor(
        _context,
        runner,
        logger);

            _headerRepository =
                new HeaderRepository(
                    _context,
                    _executor,
                    logger);

            _folderRepository =
                new FolderRepository(
                    _context,
                    _executor,
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

            foreach (DiscoveredFolder folder in folders)
            {
                try
                {
                    ProcessFolder(folder);
                }
                catch (Exception ex)
                {
                    _logger.Log(
                        "FOLDER ERROR : " +
                        ex.Message);
                }
            }

            _logger.Log("");
            _logger.Log("Inventory Completed Successfully.");
        }

        /// <summary>
        /// Processes one Year-Month folder.
        /// </summary>
        private void ProcessFolder(
            DiscoveredFolder folder)
        {
            InventoryContext context =
                BuildContext(folder);

            RegisterHeader(context);

            RegisterFolder(context);

            RegisterFiles(context);

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
        _headerRepository.GetOrCreate(header);

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

    _activityRepository.Insert(activity);

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
        _folderRepository.GetOrCreate(folder);

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

    _activityRepository.Insert(activity);

    _logger.Log(
        "FOLDER          ID : " +
        context.FolderId);
}

private void ProcessSingleFile(
    InventoryContext context,
    FileInfo file)
{
    ValidationResult validation =
        _validator.Validate(
            file.FullName);

    DetailRecord detail = BuildDetailRecord(
        context,
        file,
        validation);


    if (_detailRepository.Exists(
            context.FolderId,
            detail.FileHash))
    {
        _logger.Log(
            "Duplicate file skipped : "
            + file.Name);

        return;
    }

    if (!validation.IsValid)
    {
        HandleAutoRejectedFile(
            context,
            file,
            detail,
            validation);

        return;
    }

    HandleValidFile(
        context,
        file,
        detail,
        validation);
}

            private void RegisterFiles(
    InventoryContext context)
{
    _logger.Log("");
    _logger.Log("Starting File Inventory...");

    foreach (FileInfo file in context.Files)
    {
        try
        {
            ProcessSingleFile(
                context,
                file);
        }
        catch (Exception ex)
        {
            context.FailedFiles++;

            ActivityRecord activity =
                CreateActivity(
                    context,
                    0,
                    StatusConstants.ActivityType.Error,
                    "FAILED",
                    file.Name,
                    ex.Message);

            _activityRepository.Insert(activity);

            _logger.Log(
                "ERROR : " +
                ex.Message);
        }
    }
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

    detail.FileHash =
        HashUtil.Sha256File(
            file.FullName);

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
    FileInfo file,
    DetailRecord detail,
    ValidationResult validation)
{
    detail.FileStatus =
        StatusConstants.FileStatus.New;

    detail.ApprovalStatus =
        StatusConstants.ApprovalStatus.Pending;

    detail.AutoRejectFlag = "N";

    detail.RenameRequiredFlag = "N";

    detail.RenameStatus =
        StatusConstants.RenameStatus.NotRequired;

    long detailId =
        _detailRepository.Insert(detail);

    if (detailId <= 0)
        throw new ApplicationException(
            "Unable to register file.");

    context.SuccessfulFiles++;

    ActivityRecord activity =
        CreateActivity(
            context,
            detailId,
            StatusConstants.ActivityType.Validation,
            "SUCCESS",
            file.Name,
            "File registered.");

    _activityRepository.Insert(activity);

    _logger.Log(
        "VALID : " +
        file.Name);
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

    _logger.Log(
        "Moved to Quarantine : "
        + destination);

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
    FileInfo file,
    DetailRecord detail,
    ValidationResult validation)
{
    string quarantinePath = MoveToQuarantine(
        context,
        file);
        

    detail.CurrentPath =
        quarantinePath;

    detail.QuarantinePath =
        quarantinePath;

    detail.FileStatus =
        StatusConstants.FileStatus.AutoRejected;

    detail.AutoRejectFlag = "Y";

    detail.RenameRequiredFlag = "Y";

    detail.RenameStatus =
        StatusConstants.RenameStatus.NotRequired;

    detail.ApprovalStatus =
        StatusConstants.ApprovalStatus.RenameRequired;

    detail.ValidationMessage =
        validation.Message;

    long detailId =
        _detailRepository.Insert(detail);

    if (detailId <= 0)
        throw new ApplicationException(
            "Unable to register rejected file.");

    context.QuarantinedFiles++;

    context.AutoRejectedFiles++;

    ActivityRecord activity =
        CreateActivity(
            context,
            detailId,
            StatusConstants.ActivityType.AutoReject,
            "SUCCESS",
            file.Name,
            validation.Message);

    _activityRepository.Insert(activity);

    _logger.Log(
        "AUTO REJECT : "
        + file.Name);
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

    _logger.Log("==========================================");
}
		
	}
	
}