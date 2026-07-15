using System;
using System.Diagnostics;
using System.Threading.Tasks;

using Meduit.ShareNormalizer.Snowflake.Infrastructure;

namespace Meduit.ShareNormalizer.Snowflake.Services
{
    /// <summary>
    /// Executes the complete Snowflake ingestion workflow.
    ///
    /// Execution Order
    /// ---------------------------------------------------------
    /// 1. Inventory Service
    /// 2. Rename Service
    /// 3. Stage Upload Service
    ///
    /// Each service executes independently.
    /// Failure of one service should not prevent the remaining
    /// services from executing.
    /// </summary>
    internal sealed class WorkflowService
    {
        private readonly Config _config;

private readonly Logger _logger;

private readonly InventoryService _inventoryService;

private readonly RenameService _renameService;

private readonly StageUploadService _stageUploadService;

        public WorkflowService(
            Config config,
            Logger logger)
        {
            if (config == null)
                throw new ArgumentNullException("config");

            if (logger == null)
                throw new ArgumentNullException("logger");

            _config = config;
            _logger = logger;

            _logger.Log("Creating InventoryService...");

            _inventoryService =
    new InventoryService(
        config,
        logger);

        _logger.Log("InventoryService created.");

_logger.Log("Creating RenameService...");

_renameService =
    new RenameService(
        config,
        logger);

        _logger.Log("RenameService created.");

_logger.Log("Creating StageUploadService...");


_stageUploadService =
    new StageUploadService(
        config,
        logger);

        _logger.Log("StageUploadService created.");
        
        }

        /// <summary>
        /// Executes the complete workflow.
        /// </summary>
        public void Execute()
{
    Stopwatch watch =
        Stopwatch.StartNew();

    LogWorkflowStart();

    RunService(
        "Inventory",
        RunInventory);

    RunService(
        "Rename",
        RunRename);

    RunService(
        "Stage Upload",
        RunStageUpload);

    watch.Stop();

    LogWorkflowCompleted(
        watch.Elapsed);
}


private void RunService(
    string serviceName,
    Action action)
{
    Stopwatch watch =
        Stopwatch.StartNew();

    try
    {
        _logger.Log("");
        _logger.Log("------------------------------------------");
        _logger.Log(serviceName + " Started");
        _logger.Log("------------------------------------------");

        action();

        watch.Stop();

        _logger.Log(
            serviceName +
            " Completed (" +
            watch.Elapsed +
            ")");
    }
    catch (Exception ex)
    {
        watch.Stop();

        _logger.Log(
            serviceName +
            " FAILED");

        _logger.Log(
            ex.ToString());
    }
}


private void RunInventory()
{
    _inventoryService.Execute();
}

private void RunRename()
{
    _renameService.Execute();
}

private void RunStageUpload()
{
    _stageUploadService.Execute();
}

        

        /// <summary>
        /// Logs workflow start.
        /// </summary>
        private void LogWorkflowStart()
        {
            _logger.Log("");
_logger.Log("==================================================");
_logger.Log("MEDUIT INGESTION WORKFLOW STARTED");
_logger.Log("==================================================");
_logger.Log("Started : " + DateTime.Now);
_logger.Log("");
        }

        /// <summary>
        /// Logs workflow completion.
        /// </summary>
        private void LogWorkflowCompleted(
            TimeSpan elapsed)
        {
            _logger.Log("");

_logger.Log("==================================================");
_logger.Log("MEDUIT INGESTION WORKFLOW COMPLETED");
_logger.Log("==================================================");

_logger.Log("Duration : " + elapsed);

_logger.Log("Completed : " + DateTime.Now);

_logger.Log("==================================================");
        }

        
    }
}