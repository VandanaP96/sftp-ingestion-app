using System;
using System.Text;
using System.Collections.Generic;
using Meduit.ShareNormalizer.Snowflake.Models;

namespace Meduit.ShareNormalizer.Snowflake.Infrastructure
{
    /// <summary>
    /// Builds all SQL statements required by the SFTP ingestion process.
    /// No SQL should be hardcoded outside this class.
    /// </summary>
    internal static class SnowflakeSqlBuilder
    {
        #region HEADER

        public static string InsertHeader(
            SnowflakeConfig cfg,
            HeaderRecord header)
        {
            StringBuilder sql = new StringBuilder();

            sql.Append("INSERT INTO ");
            sql.Append(cfg.FullHeaderTable);
            sql.Append(" (");

            sql.Append("CLIENT_CODE,");
            sql.Append("CLIENT_NAME,");
            sql.Append("SOURCE_SYSTEM,");
            sql.Append("ROOT_FOLDER,");
            sql.Append("ACTIVE_FLAG,");
            sql.Append("CREATED_BY");

            sql.Append(") VALUES (");

            sql.Append(Sql(header.ClientCode));
            sql.Append(",");
            sql.Append(Sql(header.ClientName));
            sql.Append(",");
            sql.Append(Sql(header.SourceSystem));
            sql.Append(",");
            sql.Append(Sql(header.RootFolder));
            sql.Append(",");
            sql.Append(Sql(header.ActiveFlag));
            sql.Append(",");
            sql.Append(Sql(header.CreatedBy));

            sql.Append(");");

            return sql.ToString();
        }

        #endregion

        #region HEADER LOOKUP

        public static string GetHeaderId(
    SnowflakeConfig cfg,
    string clientCode,
    string sourceSystem,
    string rootFolder)
{
    StringBuilder sql = new StringBuilder();

    sql.Append("SELECT HEADER_ID ");

    sql.Append("FROM ");

    sql.Append(cfg.FullHeaderTable);

    sql.Append(" WHERE ");

    sql.Append("CLIENT_CODE=");
    sql.Append(Sql(clientCode));

    sql.Append(" AND ");

    sql.Append("SOURCE_SYSTEM=");
    sql.Append(Sql(sourceSystem));

    sql.Append(" AND ");

    sql.Append("ROOT_FOLDER=");
    sql.Append(Sql(rootFolder));

    sql.Append(" AND ACTIVE_FLAG='Y'");

    sql.Append(" LIMIT 1;");

    return sql.ToString();
}

        #endregion

        #region HEADER EXISTS

        public static string HeaderExists(
    SnowflakeConfig cfg,
    string clientCode,
    string sourceSystem,
    string rootFolder)
{
    StringBuilder sql = new StringBuilder();

    sql.Append("SELECT COUNT(*) ");

    sql.Append("FROM ");

    sql.Append(cfg.FullHeaderTable);

    sql.Append(" WHERE ");

    sql.Append("CLIENT_CODE=");
    sql.Append(Sql(clientCode));

    sql.Append(" AND ");

    sql.Append("SOURCE_SYSTEM=");
    sql.Append(Sql(sourceSystem));

    sql.Append(" AND ");

    sql.Append("ROOT_FOLDER=");
    sql.Append(Sql(rootFolder));

    sql.Append(" AND ACTIVE_FLAG='Y';");

    return sql.ToString();
}

        #endregion

        #region UPDATE HEADER

        public static string UpdateHeaderAudit(
            SnowflakeConfig cfg,
            long headerId,
            string updatedBy)
        {
            StringBuilder sql = new StringBuilder();

            sql.Append("UPDATE ");
            sql.Append(cfg.FullHeaderTable);

            sql.Append(" SET ");

            sql.Append("UPDATED_BY=");
            sql.Append(Sql(updatedBy));

            sql.Append(",");

            sql.Append("UPDATED_DATE=CURRENT_TIMESTAMP()");

            sql.Append(" WHERE HEADER_ID=");
            sql.Append(headerId);

            sql.Append(";");

            return sql.ToString();
        }

        #endregion

        #region FOLDER

        public static string InsertFolder(
            SnowflakeConfig cfg,
            FolderRecord folder)
        {
            StringBuilder sql = new StringBuilder();

            sql.Append("INSERT INTO ");
            sql.Append(cfg.FullFolderTable);

            sql.Append(" (");

            sql.Append("HEADER_ID,");
            sql.Append("YEAR_MONTH,");
            sql.Append("FOLDER_NAME,");
            sql.Append("FOLDER_PATH,");
            sql.Append("FOLDER_HASH,");
            sql.Append("FOLDER_STATUS,");
            sql.Append("SCANNED_DATE,");
            sql.Append("ACTIVE_FLAG,");
            sql.Append("CREATED_BY");

            sql.Append(") VALUES (");

            sql.Append(folder.HeaderId);
            sql.Append(",");

            sql.Append(Sql(folder.YearMonth));
            sql.Append(",");

            sql.Append(Sql(folder.FolderName));
            sql.Append(",");

            sql.Append(Sql(folder.FolderPath));
            sql.Append(",");

            sql.Append(Sql(folder.FolderHash));
            sql.Append(",");

            sql.Append(Sql(folder.FolderStatus));
            sql.Append(",");

            sql.Append("CURRENT_TIMESTAMP()");
            sql.Append(",");

            sql.Append(Sql(folder.ActiveFlag));
            sql.Append(",");

            sql.Append(Sql(folder.CreatedBy));

            sql.Append(");");

            return sql.ToString();
        }

        public static string FolderExists(
            SnowflakeConfig cfg,
            long headerId,
            string folderHash)
        {
            StringBuilder sql = new StringBuilder();

            sql.Append("SELECT COUNT(*) ");

            sql.Append("FROM ");

            sql.Append(cfg.FullFolderTable);

            sql.Append(" WHERE ");

            sql.Append("HEADER_ID=");
            sql.Append(headerId);

            sql.Append(" AND ");

            sql.Append("FOLDER_HASH=");
            sql.Append(Sql(folderHash));

            sql.Append(" AND ACTIVE_FLAG='Y';");

            return sql.ToString();
        }

        public static string GetFolderId(
            SnowflakeConfig cfg,
            long headerId,
            string folderHash)
        {
            StringBuilder sql = new StringBuilder();

            sql.Append("SELECT FOLDER_ID ");

            sql.Append("FROM ");

            sql.Append(cfg.FullFolderTable);

            sql.Append(" WHERE ");

            sql.Append("HEADER_ID=");
            sql.Append(headerId);

            sql.Append(" AND ");

            sql.Append("FOLDER_HASH=");
            sql.Append(Sql(folderHash));

            sql.Append(" LIMIT 1;");

            return sql.ToString();
        }

        public static string UpdateFolderStatus(
            SnowflakeConfig cfg,
            long folderId,
            string status)
        {
            StringBuilder sql = new StringBuilder();

            sql.Append("UPDATE ");

            sql.Append(cfg.FullFolderTable);

            sql.Append(" SET ");

            sql.Append("FOLDER_STATUS=");
            sql.Append(Sql(status));

            sql.Append(",");

            sql.Append("SCANNED_DATE=CURRENT_TIMESTAMP()");

            sql.Append(" WHERE ");

            sql.Append("FOLDER_ID=");
            sql.Append(folderId);

            sql.Append(";");

            return sql.ToString();
        }

        public static string UpdateFolderApproval(
    SnowflakeConfig cfg,
    long folderId,
    string status,
    string approvedBy)
{
    StringBuilder sql = new StringBuilder();

    sql.Append("UPDATE ");
    sql.Append(cfg.FullFolderTable);

    sql.Append(" SET ");

    sql.Append("FOLDER_STATUS=");
    sql.Append(Sql(status));

    sql.Append(",");

    sql.Append("APPROVED_BY=");
    sql.Append(Sql(approvedBy));

    sql.Append(",");

    sql.Append("APPROVED_DATE=CURRENT_TIMESTAMP()");

    sql.Append(" WHERE ");

    sql.Append("FOLDER_ID=");
    sql.Append(folderId);

    sql.Append(";");

    return sql.ToString();
}

        #endregion

                #region DETAIL

        public static string InsertDetail(
    SnowflakeConfig cfg,
    DetailRecord detail)
{
    StringBuilder sql = new StringBuilder();

    sql.Append("INSERT INTO ");
    sql.Append(cfg.FullDetailTable);

    sql.Append(" (");

    sql.Append("FOLDER_ID,");

    sql.Append("ORIGINAL_FILE_NAME,");

    sql.Append("CURRENT_FILE_NAME,");

    sql.Append("FILE_TYPE,");

    sql.Append("FILE_EXTENSION,");

    sql.Append("ORIGINAL_PATH,");

    sql.Append("CURRENT_PATH,");

    sql.Append("QUARANTINE_PATH,");

    sql.Append("STAGE_PATH,");

    sql.Append("ARCHIVE_PATH,");

    sql.Append("FILE_SIZE_KB,");

    sql.Append("LAST_MODIFIED,");

    sql.Append("FILE_HASH,");

    sql.Append("DATE_PATTERN,");

    sql.Append("VALID_DATE_FLAG,");

    sql.Append("VALIDATION_MESSAGE,");

    sql.Append("FILE_STATUS,");

    sql.Append("AUTO_REJECT_FLAG,");

    sql.Append("APPROVAL_STATUS,");

    sql.Append("RENAME_REQUIRED_FLAG,");

    sql.Append("RENAME_STATUS,");

    sql.Append("INGESTION_STATUS,");

    sql.Append("ROW_COUNT,");

    sql.Append("ERROR_MESSAGE");

    sql.Append(") VALUES (");

    sql.Append(detail.FolderId);

    sql.Append(",");

    sql.Append(Sql(detail.OriginalFileName));

    sql.Append(",");

    sql.Append(Sql(detail.CurrentFileName));

    sql.Append(",");

    sql.Append(Sql(detail.FileType));

    sql.Append(",");

    sql.Append(Sql(detail.FileExtension));

    sql.Append(",");

    sql.Append(Sql(detail.OriginalPath));

    sql.Append(",");

    sql.Append(Sql(detail.CurrentPath));

    sql.Append(",");

    sql.Append(Sql(detail.QuarantinePath));

    sql.Append(",");

    sql.Append(Sql(detail.StagePath));

    sql.Append(",");

    sql.Append(Sql(detail.ArchivePath));

    sql.Append(",");

    sql.Append(detail.FileSizeKb);

    sql.Append(",");

    sql.Append(Sql(detail.LastModified));

    sql.Append(",");

    sql.Append(Sql(detail.FileHash));

    sql.Append(",");

    sql.Append(Sql(detail.DatePattern));

    sql.Append(",");

    sql.Append(Sql(detail.ValidDateFlag));

    sql.Append(",");

    sql.Append(Sql(detail.ValidationMessage));

    sql.Append(",");

    sql.Append(Sql(detail.FileStatus));

    sql.Append(",");

    sql.Append(Sql(detail.AutoRejectFlag));

    sql.Append(",");

    sql.Append(Sql(detail.ApprovalStatus));

    sql.Append(",");

    sql.Append(Sql(detail.RenameRequiredFlag));

    sql.Append(",");

    sql.Append(Sql(detail.RenameStatus));

    sql.Append(",");

    sql.Append(Sql(detail.IngestionStatus));

    sql.Append(",");

    sql.Append(detail.RowCount);

    sql.Append(",");

    sql.Append(Sql(detail.ErrorMessage));

    sql.Append(");");

    return sql.ToString();
}

public static string InsertDetailBatch(
    SnowflakeConfig cfg,
    IEnumerable<DetailRecord> records)
{
    StringBuilder sql =
        new StringBuilder();

    sql.Append(
        "INSERT INTO ");

    sql.Append(cfg.FullDetailTable);

    sql.AppendLine();

    sql.AppendLine("(");

    sql.AppendLine(
        "FOLDER_ID,");

    sql.AppendLine(
        "ORIGINAL_FILE_NAME,");

    sql.AppendLine(
        "CURRENT_FILE_NAME,");

    sql.AppendLine(
        "FILE_EXTENSION,");

    sql.AppendLine(
        "FILE_HASH");

    sql.AppendLine(")");

    sql.AppendLine("VALUES");

    bool first = true;

    foreach (DetailRecord r in records)
    {
        if (!first)
        {
            sql.AppendLine(",");
        }

        first = false;

        sql.Append("(");

        sql.Append(r.FolderId);

        sql.Append(",");

        sql.Append(Sql(r.OriginalFileName));

        sql.Append(",");

        sql.Append(Sql(r.CurrentFileName));

        sql.Append(",");

        sql.Append(Sql(r.FileExtension));

        sql.Append(",");

        sql.Append(Sql(r.FileHash));

        sql.Append(")");
    }

    sql.Append(";");

    return sql.ToString();
}



        public static string GetDetailId(
    SnowflakeConfig cfg,
    long folderId,
    string fileHash)
{
    StringBuilder sql = new StringBuilder();

    sql.Append("SELECT DETAIL_ID ");

    sql.Append("FROM ");

    sql.Append(cfg.FullDetailTable);

    sql.Append(" WHERE ");

    sql.Append("FOLDER_ID=");

    sql.Append(folderId);

    sql.Append(" AND ");

    sql.Append("FILE_HASH=");

    sql.Append(Sql(fileHash));

    sql.Append(" LIMIT 1;");

    return sql.ToString();
}

public static string GetDetailIdsBatch(
    SnowflakeConfig cfg,
    IEnumerable<string> hashes)
{
    StringBuilder sql =
        new StringBuilder();

    sql.Append("SELECT DETAIL_ID,FILE_HASH ");

    sql.Append("FROM ");

    sql.Append(cfg.FullDetailTable);

    sql.Append(" WHERE FILE_HASH IN (");

    bool first=true;

    foreach(string hash in hashes)
    {
        if(!first)
            sql.Append(",");

        first=false;

        sql.Append(Sql(hash));
    }

    sql.Append(")");

    return sql.ToString();
}

        public static string DetailExists(
            SnowflakeConfig cfg,
            long folderId,
            string fileHash)
        {
            StringBuilder sql = new StringBuilder();

            sql.Append("SELECT COUNT(*) ");

            sql.Append("FROM ");

            sql.Append(cfg.FullDetailTable);

            sql.Append(" WHERE ");

            sql.Append("FOLDER_ID=");
            sql.Append(folderId);

            sql.Append(" AND ");

            sql.Append("FILE_HASH=");
            sql.Append(Sql(fileHash));

            sql.Append(";");

            return sql.ToString();
        }

        public static string DetailExistsBatch(
    SnowflakeConfig cfg,
    IEnumerable<string> hashes)
{
    StringBuilder sql =
        new StringBuilder();

    sql.Append("SELECT FILE_HASH ");
    sql.Append("FROM ");
    sql.Append(cfg.FullDetailTable);
    sql.Append(" WHERE FILE_HASH IN (");

    bool first = true;

    foreach (string hash in hashes)
    {
        if (!first)
        {
            sql.Append(",");
        }

        first = false;

        sql.Append(Sql(hash));
    }

    sql.Append(")");

    return sql.ToString();
}

        public static string UpdateApprovalStatus(
            SnowflakeConfig cfg,
            long detailId,
            string status,
            string approvedBy)
        {
            StringBuilder sql = new StringBuilder();

            sql.Append("UPDATE ");

            sql.Append(cfg.FullDetailTable);

            sql.Append(" SET ");

            sql.Append("APPROVAL_STATUS=");
            sql.Append(Sql(status));

            sql.Append(",");

            sql.Append("APPROVED_BY=");
            sql.Append(Sql(approvedBy));

            sql.Append(",");

            sql.Append("APPROVED_DATE=CURRENT_TIMESTAMP()");

            sql.Append(" WHERE DETAIL_ID=");

            sql.Append(detailId);

            sql.Append(";");

            return sql.ToString();
        }

        public static string UpdateCurrentFileName(
    SnowflakeConfig cfg,
    long detailId,
    string currentFileName)
{
    StringBuilder sql = new StringBuilder();

    sql.Append("UPDATE ");
    sql.Append(cfg.FullDetailTable);

    sql.Append(" SET ");

    sql.Append("CURRENT_FILE_NAME=");
    sql.Append(Sql(currentFileName));

    sql.Append(",");

    sql.Append("UPDATED_DATE=CURRENT_TIMESTAMP()");

    sql.Append(" WHERE DETAIL_ID=");
    sql.Append(detailId);

    sql.Append(";");

    return sql.ToString();
}

        public static string UpdateCurrentPath(
    SnowflakeConfig cfg,
    long detailId,
    string currentPath)
{
    StringBuilder sql = new StringBuilder();

    sql.Append("UPDATE ");
    sql.Append(cfg.FullDetailTable);

    sql.Append(" SET ");

    sql.Append("CURRENT_PATH=");
    sql.Append(Sql(currentPath));

    sql.Append(",");

    sql.Append("UPDATED_DATE=CURRENT_TIMESTAMP()");

    sql.Append(" WHERE DETAIL_ID=");
    sql.Append(detailId);

    sql.Append(";");

    return sql.ToString();
}


        public static string ClearQuarantinePath(
    SnowflakeConfig cfg,
    long detailId)
{
    StringBuilder sql = new StringBuilder();

    sql.Append("UPDATE ");
    sql.Append(cfg.FullDetailTable);

    sql.Append(" SET ");

    sql.Append("QUARANTINE_PATH=NULL,");

    sql.Append("UPDATED_DATE=CURRENT_TIMESTAMP()");

    sql.Append(" WHERE DETAIL_ID=");
    sql.Append(detailId);

    sql.Append(";");

    return sql.ToString();
}

public static string FinishRename(
    SnowflakeConfig cfg,
    long detailId,
    string currentFileName,
    string currentPath)
{
    StringBuilder sql = new StringBuilder();

    sql.Append("UPDATE ");
    sql.Append(cfg.FullDetailTable);

    sql.Append(" SET ");

    sql.Append("CURRENT_FILE_NAME=");
    sql.Append(Sql(currentFileName));

    sql.Append(",");

    sql.Append("CURRENT_PATH=");
    sql.Append(Sql(currentPath));

    sql.Append(",");

    sql.Append("RENAME_STATUS='COMPLETED'");

    sql.Append(",");

    sql.Append("FILE_STATUS='READY_FOR_UPLOAD'");

    sql.Append(",");

    sql.Append("UPDATED_DATE=CURRENT_TIMESTAMP()");

    sql.Append(" WHERE DETAIL_ID=");
    sql.Append(detailId);

    sql.Append(";");

    return sql.ToString();
}

        public static string CompleteRename(
    SnowflakeConfig cfg,
    long detailId)
{
    StringBuilder sql = new StringBuilder();

    sql.Append("UPDATE ");
    sql.Append(cfg.FullDetailTable);

    sql.Append(" SET ");

    sql.Append("RENAME_STATUS='COMPLETED',");

    sql.Append("FILE_STATUS='PENDING_APPROVAL',");

    sql.Append("UPDATED_DATE=CURRENT_TIMESTAMP()");

    sql.Append(" WHERE DETAIL_ID=");
    sql.Append(detailId);

    sql.Append(";");

    return sql.ToString();
}

public static string GetStageUploadJobs(
    SnowflakeConfig cfg)
{
    StringBuilder sql =
        new StringBuilder();

    sql.Append("SELECT ");

    sql.Append("DETAIL_ID,");

    sql.Append("FOLDER_ID,");

    sql.Append("CURRENT_FILE_NAME,");

    sql.Append("CURRENT_PATH,");

    sql.Append("STAGE_PATH,");

    sql.Append("ARCHIVE_PATH ");

    sql.Append("FROM ");

    sql.Append(cfg.FullDetailTable);

    sql.Append(" WHERE ");

sql.Append("APPROVAL_STATUS='APPROVED' ");

sql.Append("AND (");

sql.Append("RENAME_STATUS='NOT_REQUIRED' ");

sql.Append("OR ");

sql.Append("RENAME_STATUS='COMPLETED'");

sql.Append(") ");

sql.Append("AND ");

sql.Append("INGESTION_STATUS='NOT_STARTED' ");

            sql.Append("AND (");

            sql.Append("FILE_STATUS='READY_FOR_UPLOAD'");

            sql.Append("OR ");

            sql.Append("FILE_STATUS='APPROVED'");

            sql.Append(") ");


            sql.Append("ORDER BY DETAIL_ID;");

    return sql.ToString();
}


        public static string GetRenameFiles(
    SnowflakeConfig cfg)
{
    StringBuilder sql = new StringBuilder();

    sql.Append("SELECT ");

    sql.Append("DETAIL_ID,");

    sql.Append("ORIGINAL_FILE_NAME,");

    sql.Append("CURRENT_FILE_NAME,");

    sql.Append("ORIGINAL_PATH,");

    sql.Append("CURRENT_PATH,");

    sql.Append("QUARANTINE_PATH,");

    sql.Append("APPROVED_BY ");

    sql.Append("FROM ");

    sql.Append(cfg.FullDetailTable);

    sql.Append(" WHERE ");

    sql.Append("RENAME_STATUS='READY' ");

    sql.Append("AND ");

    sql.Append("FILE_STATUS='AUTO_REJECTED';");

    return sql.ToString();
}

        public static string GetRenameJobs(
    SnowflakeConfig cfg)
{
    StringBuilder sql =
        new StringBuilder();

    sql.Append("SELECT ");

    sql.Append("DETAIL_ID,");

    sql.Append("FOLDER_ID,");

    sql.Append("ORIGINAL_FILE_NAME,");

    sql.Append("CURRENT_FILE_NAME,");

    sql.Append("ORIGINAL_PATH,");

    sql.Append("CURRENT_PATH,");

    sql.Append("QUARANTINE_PATH,");

    sql.Append("APPROVED_BY ");

    sql.Append("FROM ");

    sql.Append(cfg.FullDetailTable);

    sql.Append(" WHERE ");

    sql.Append("RENAME_STATUS='READY' ");

    sql.Append("AND ");

    sql.Append("APPROVAL_STATUS='APPROVED' ");

    sql.Append("AND ");

    sql.Append("FILE_STATUS='AUTO_REJECTED';");

    return sql.ToString();
}

public static string FinishUpload(
    SnowflakeConfig cfg,
    long detailId,
    string stagePath,
    string archivePath)
{
    StringBuilder sql =
        new StringBuilder();

    sql.Append("UPDATE ");
    sql.Append(cfg.FullDetailTable);

    sql.Append(" SET ");

    sql.Append("STAGE_PATH=");
    sql.Append(Sql(stagePath));

    sql.Append(",");

    sql.Append("ARCHIVE_PATH=");
    sql.Append(Sql(archivePath));

    sql.Append(",");

    sql.Append("CURRENT_PATH=");
    sql.Append(Sql(archivePath));

    sql.Append(",");

    sql.Append("FILE_STATUS='ARCHIVED',");

    sql.Append("INGESTION_STATUS='COMPLETED',");

            sql.Append("ARCHIVED_DATE=CURRENT_TIMESTAMP(),");

            sql.Append("INGESTION_END_TIME=CURRENT_TIMESTAMP(),");

    sql.Append("UPDATED_DATE=CURRENT_TIMESTAMP()");

    sql.Append(" WHERE DETAIL_ID=");

    sql.Append(detailId);

    sql.Append(";");

    return sql.ToString();
}



        public static string GetApprovedFiles(
    SnowflakeConfig cfg)
{
    StringBuilder sql = new StringBuilder();

    sql.Append("SELECT ");

    sql.Append("DETAIL_ID,");

    sql.Append("CURRENT_FILE_NAME,");

    sql.Append("CURRENT_PATH,");

    sql.Append("STAGE_PATH,");

    sql.Append("ARCHIVE_PATH ");

    sql.Append("FROM ");

    sql.Append(cfg.FullDetailTable);

    sql.Append(" WHERE ");

    sql.Append("APPROVAL_STATUS='APPROVED' ");

    sql.Append("AND ");

    sql.Append("INGESTION_STATUS='NOT_STARTED';");

    return sql.ToString();
}


        public static string UpdateRename(
    SnowflakeConfig cfg,
    long detailId,
    string currentFileName,
    string currentPath,
    string approvedBy)
{
    StringBuilder sql = new StringBuilder();

    sql.Append("UPDATE ");

    sql.Append(cfg.FullDetailTable);

    sql.Append(" SET ");

    sql.Append("CURRENT_FILE_NAME=");
    sql.Append(Sql(currentFileName));

    sql.Append(",");

    sql.Append("CURRENT_PATH=");
    sql.Append(Sql(currentPath));

    sql.Append(",");

    sql.Append("RENAME_STATUS='COMPLETED',");

    sql.Append("RENAME_REQUIRED_FLAG='N',");

    sql.Append("AUTO_REJECT_FLAG='N',");

    sql.Append("APPROVAL_STATUS='APPROVED',");

    sql.Append("FILE_STATUS='PENDING_APPROVAL',");

    sql.Append("RENAMED_BY=");
    sql.Append(Sql(approvedBy));

    sql.Append(",");

    sql.Append("RENAMED_DATE=CURRENT_TIMESTAMP()");

    sql.Append(" WHERE DETAIL_ID=");

    sql.Append(detailId);

    sql.Append(";");

    return sql.ToString();
}

        public static string UpdateQuarantinePath(
    SnowflakeConfig cfg,
    long detailId,
    string quarantinePath)
{
    StringBuilder sql = new StringBuilder();

    sql.Append("UPDATE ");

    sql.Append(cfg.FullDetailTable);

    sql.Append(" SET ");

    sql.Append("QUARANTINE_PATH=");

    sql.Append(Sql(quarantinePath));

    sql.Append(" WHERE DETAIL_ID=");

    sql.Append(detailId);

    sql.Append(";");

    return sql.ToString();
}

        

        public static string UpdateArchivePath(
    SnowflakeConfig cfg,
    long detailId,
    string archivePath)
{
    StringBuilder sql = new StringBuilder();

    sql.Append("UPDATE ");

    sql.Append(cfg.FullDetailTable);

    sql.Append(" SET ");

    sql.Append("ARCHIVE_PATH=");

    sql.Append(Sql(archivePath));

    sql.Append(",");

    sql.Append("ARCHIVED_DATE=CURRENT_TIMESTAMP()");

    sql.Append(" WHERE DETAIL_ID=");

    sql.Append(detailId);

    sql.Append(";");

    return sql.ToString();
}



        public static string UpdateIngestionStatus(
    SnowflakeConfig cfg,
    long detailId,
    string status,
    long rowCount)
{
    StringBuilder sql = new StringBuilder();

    sql.Append("UPDATE ");
    sql.Append(cfg.FullDetailTable);

    sql.Append(" SET ");

    sql.Append("INGESTION_STATUS=");
    sql.Append(Sql(status));

    sql.Append(",");

    sql.Append("ROW_COUNT=");
    sql.Append(rowCount);

    sql.Append(",");

    sql.Append("INGESTION_END_TIME=CURRENT_TIMESTAMP()");

    sql.Append(" WHERE DETAIL_ID=");
    sql.Append(detailId);

    sql.Append(";");

    return sql.ToString();
}

        public static string UpdateIngestionStart(
            SnowflakeConfig cfg,
            long detailId)
        {
            StringBuilder sql = new StringBuilder();

            sql.Append("UPDATE ");

            sql.Append(cfg.FullDetailTable);

            sql.Append(" SET ");

            sql.Append(" INGESTION_STATUS='IN_PROGRESS', ");

            sql.Append("INGESTION_START_TIME=CURRENT_TIMESTAMP()");

            sql.Append(" WHERE DETAIL_ID=");

            sql.Append(detailId);

            sql.Append(";");

            return sql.ToString();
        }

        public static string UpdateError(
    SnowflakeConfig cfg,
    long detailId,
    string error)
{
    StringBuilder sql = new StringBuilder();

    sql.Append("UPDATE ");
    sql.Append(cfg.FullDetailTable);

    sql.Append(" SET ");

    sql.Append("FILE_STATUS='FAILED',");

    sql.Append("INGESTION_STATUS='FAILED',");

    sql.Append("ERROR_MESSAGE=");
    sql.Append(Sql(error));

    sql.Append(",");

    sql.Append("INGESTION_END_TIME=CURRENT_TIMESTAMP()");

    sql.Append(" WHERE DETAIL_ID=");
    sql.Append(detailId);

    sql.Append(";");

    return sql.ToString();
}

        #endregion

                #region ACTIVITY

        /// <summary>
        /// Inserts a processing activity into FILE_ACTIVITY_LOG.
        /// </summary>
        public static string InsertActivity(
            SnowflakeConfig cfg,
            ActivityRecord activity)
        {
            StringBuilder sql = new StringBuilder();

            sql.Append("INSERT INTO ");
            sql.Append(cfg.FullActivityTable);

            sql.Append(" (");

            sql.Append("HEADER_ID,");
            sql.Append("FOLDER_ID,");
            sql.Append("DETAIL_ID,");
            sql.Append("ACTIVITY_TYPE,");
            sql.Append("ACTIVITY_STATUS,");
            sql.Append("ACTIVITY_MESSAGE,");
            sql.Append("EXECUTED_BY,");
            sql.Append("DURATION_SECONDS,");
            sql.Append("ERROR_CODE,");
            sql.Append("ERROR_MESSAGE");

            sql.Append(") VALUES (");

            sql.Append(activity.HeaderId);
            sql.Append(",");

            sql.Append(activity.FolderId);
            sql.Append(",");

            sql.Append(activity.DetailId);
            sql.Append(",");

            sql.Append(Sql(activity.ActivityType));
            sql.Append(",");

            sql.Append(Sql(activity.ActivityStatus));
            sql.Append(",");

            sql.Append(Sql(activity.ActivityMessage));
            sql.Append(",");

            sql.Append(Sql(activity.ExecutedBy));
            sql.Append(",");

            sql.Append(activity.DurationSeconds);
            sql.Append(",");

            sql.Append(Sql(activity.ErrorCode));
            sql.Append(",");

            sql.Append(Sql(activity.ErrorMessage));

            sql.Append(");");

            return sql.ToString();
        }

        /// <summary>
        /// Update activity status.
        /// </summary>
        public static string UpdateActivityStatus(
            SnowflakeConfig cfg,
            long activityId,
            string status)
        {
            StringBuilder sql = new StringBuilder();

            sql.Append("UPDATE ");

            sql.Append(cfg.FullActivityTable);

            sql.Append(" SET ");

            sql.Append("ACTIVITY_STATUS=");
            sql.Append(Sql(status));

            sql.Append(",");

            sql.Append("EXECUTED_TIME=CURRENT_TIMESTAMP()");

            sql.Append(" WHERE ");

            sql.Append("ACTIVITY_ID=");
            sql.Append(activityId);

            sql.Append(";");

            return sql.ToString();
        }

        /// <summary>
        /// Update activity error.
        /// </summary>
        public static string UpdateActivityError(
            SnowflakeConfig cfg,
            long activityId,
            string errorCode,
            string errorMessage)
        {
            StringBuilder sql = new StringBuilder();

            sql.Append("UPDATE ");

            sql.Append(cfg.FullActivityTable);

            sql.Append(" SET ");

            sql.Append("ACTIVITY_STATUS='FAILED',");

            sql.Append("ERROR_CODE=");
            sql.Append(Sql(errorCode));

            sql.Append(",");

            sql.Append("ERROR_MESSAGE=");
            sql.Append(Sql(errorMessage));

            sql.Append(",");

            sql.Append("EXECUTED_TIME=CURRENT_TIMESTAMP()");

            sql.Append(" WHERE ");

            sql.Append("ACTIVITY_ID=");
            sql.Append(activityId);

            sql.Append(";");

            return sql.ToString();
        }

        #endregion

        #region COMMON

        private static string Sql(string value)
        {
            if (value == null)
                return "NULL";

            return "'" +
                   value
                       .Replace("\\", "\\\\")
                       .Replace("'", "''")
                   + "'";
        }

        private static string NormalizeSqlString(string value)
        {
            if (value == null)
                return "";

            value = value.Replace("\r", "");
            value = value.Replace("\n", "");

            value = value.Replace("'", "''");

            return value;
        }

        private static string Sql(DateTime? value)
{
    if (!value.HasValue)
        return "NULL";

    return "'" + value.Value.ToString("yyyy-MM-dd HH:mm:ss") + "'";
}

private static string Sql(bool value)
{
    return value ? "'Y'" : "'N'";
}

#endregion

        
    }
}