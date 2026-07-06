using System;
using System.Diagnostics;

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
        }

        /// <summary>
        /// Executes the complete workflow.
        /// </summary>
        public void Execute()
        {
            Stopwatch workflowWatch =
                Stopwatch.StartNew();

            LogWorkflowStart();

            ExecuteInventory();

            ExecuteRename();

            ExecuteStageUpload();

            workflowWatch.Stop();

            LogWorkflowCompleted(
                workflowWatch.Elapsed);
        }

        /// <summary>
        /// Executes InventoryService.
        /// </summary>
        private void ExecuteInventory()
        {
            Stopwatch watch =
                Stopwatch.StartNew();

            try
            {
                _logger.Log("");
                _logger.Log("--------------------------------------------------");
                _logger.Log("STEP 1 : Inventory Service");
                _logger.Log("--------------------------------------------------");

                InventoryService service =
                    new InventoryService(
                        _config,
                        _logger);

                service.Execute();

                watch.Stop();

                _logger.Log("");

                _logger.Log(
                    string.Format(
                        "Inventory completed in {0}",
                        watch.Elapsed));
            }
            catch (Exception ex)
            {
                watch.Stop();

                _logger.Log("");

                _logger.Log(
                    "Inventory Service Failed");

                _logger.Log(
                    ex.ToString());
            }
        }

        /// <summary>
        /// Executes RenameService.
        /// </summary>
        private void ExecuteRename()
        {
            Stopwatch watch =
                Stopwatch.StartNew();

            try
            {
                _logger.Log("");
                _logger.Log("--------------------------------------------------");
                _logger.Log("STEP 2 : Rename Service");
                _logger.Log("--------------------------------------------------");

                RenameService service =
                    new RenameService(
                        _config,
                        _logger);

                service.Execute();

                watch.Stop();

                _logger.Log("");

                _logger.Log(
                    string.Format(
                        "Rename completed in {0}",
                        watch.Elapsed));
            }
            catch (Exception ex)
            {
                watch.Stop();

                _logger.Log("");

                _logger.Log(
                    "Rename Service Failed");

                _logger.Log(
                    ex.ToString());
            }
        }


                /// <summary>
        /// Executes StageUploadService.
        /// </summary>
        private void ExecuteStageUpload()
        {
            Stopwatch watch =
                Stopwatch.StartNew();

            try
            {
                _logger.Log("");
                _logger.Log("--------------------------------------------------");
                _logger.Log("STEP 3 : Stage Upload Service");
                _logger.Log("--------------------------------------------------");

                StageUploadService service =
                    new StageUploadService(
                        _config,
                        _logger);

                service.Execute();

                watch.Stop();

                _logger.Log("");

                _logger.Log(
                    string.Format(
                        "Stage Upload completed in {0}",
                        watch.Elapsed));
            }
            catch (Exception ex)
            {
                watch.Stop();

                _logger.Log("");

                _logger.Log(
                    "Stage Upload Service Failed");

                _logger.Log(
                    ex.ToString());
            }
        }

        /// <summary>
        /// Logs workflow start.
        /// </summary>
        private void LogWorkflowStart()
        {
            _logger.Log("");
            _logger.Log("==============================================================");
            _logger.Log("        MEDUIT SFTP INGESTION WORKFLOW STARTED");
            _logger.Log("==============================================================");
            _logger.Log("Execution Time : " + DateTime.Now);
            _logger.Log("");
        }

        /// <summary>
        /// Logs workflow completion.
        /// </summary>
        private void LogWorkflowCompleted(
            TimeSpan elapsed)
        {
            _logger.Log("");

            _logger.Log("==============================================================");
            _logger.Log("        MEDUIT SFTP INGESTION WORKFLOW COMPLETED");
            _logger.Log("==============================================================");

            _logger.Log("");

            _logger.Log(
                "Total Duration : " +
                elapsed);

            _logger.Log(
                "Completed At   : " +
                DateTime.Now);

            _logger.Log("");

            _logger.Log("Workflow finished successfully.");

            _logger.Log("==============================================================");
        }

        /// <summary>
        /// Writes a separator line.
        /// </summary>
        private void LogSeparator()
        {
            _logger.Log(
                "--------------------------------------------------");
        }
    }
}