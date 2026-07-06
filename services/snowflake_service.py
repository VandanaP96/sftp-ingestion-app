"""
services/snowflake_services.py
All Snowflake read/write operations — schema v2 (3-status model)
"""

import pandas as pd
from snowflake.snowpark.context import get_active_session

session = get_active_session()
DB = "MEDUIT_DEX.SFTP_INGESTION"


def sql(query: str) -> pd.DataFrame:
    return session.sql(query).to_pandas()

def execute(query: str):
    session.sql(query).collect()


def get_clients() -> pd.DataFrame:
    return sql(f"""
        SELECT HEADER_ID, CLIENT_CODE, CLIENT_NAME
        FROM   {DB}.FILE_BATCH_HEADER
        WHERE  ACTIVE_FLAG = 'Y'
        ORDER  BY CLIENT_NAME
    """)


def get_folders(header_id: int) -> pd.DataFrame:
    return sql(f"""
        SELECT FOLDER_ID, YEAR_MONTH, FOLDER_PATH, FOLDER_STATUS, SCANNED_DATE
        FROM   {DB}.FILE_BATCH_FOLDER
        WHERE  HEADER_ID   = {header_id}
          AND  ACTIVE_FLAG = 'Y'
        ORDER  BY YEAR_MONTH DESC
    """)


def get_files(folder_id: int) -> pd.DataFrame:
    return sql(f"""
        SELECT DETAIL_ID,
               ORIGINAL_FILE_NAME,
               CURRENT_FILE_NAME,
               FILE_TYPE,
               FILE_EXTENSION,
               FILE_SIZE_KB,
               VALID_DATE_FLAG,
               VALIDATION_MESSAGE,
               FILE_STATUS,
               AUTO_REJECT_FLAG,
               APPROVAL_STATUS,
               RENAME_REQUIRED_FLAG,
               RENAME_STATUS,
               APPROVED_BY,
               APPROVED_DATE,
               RENAMED_BY,
               RENAMED_DATE,
               QUARANTINE_PATH
        FROM   {DB}.FILE_BATCH_DETAIL
        WHERE  FOLDER_ID = {folder_id}
        ORDER  BY APPROVAL_STATUS, ORIGINAL_FILE_NAME
    """)


def get_approval_counts(folder_id: int) -> dict:
    # APPROVAL_STATUS counts
    df = sql(f"""
        SELECT APPROVAL_STATUS AS STATUS, COUNT(*) AS CNT
        FROM   {DB}.FILE_BATCH_DETAIL
        WHERE  FOLDER_ID = {folder_id}
        GROUP  BY APPROVAL_STATUS
    """)
    counts = df.set_index("STATUS")["CNT"].to_dict() if not df.empty else {}

    # AUTO_REJECTED comes from FILE_STATUS, not APPROVAL_STATUS
    auto = sql(f"""
        SELECT COUNT(*) AS CNT
        FROM   {DB}.FILE_BATCH_DETAIL
        WHERE  FOLDER_ID     = {folder_id}
          AND  FILE_STATUS   = 'AUTO_REJECTED'
    """)
    counts["AUTO_REJECTED"] = int(auto.iloc[0]["CNT"]) if not auto.empty else 0
    return counts


def approve_all(folder_id: int, header_id: int, user: str):
    execute(f"""
        UPDATE {DB}.FILE_BATCH_DETAIL
        SET    APPROVAL_STATUS = 'APPROVED',
               APPROVED_BY    = '{user}',
               APPROVED_DATE  = CURRENT_TIMESTAMP(),
               UPDATED_DATE   = CURRENT_TIMESTAMP()
        WHERE  FOLDER_ID       = {folder_id}
          AND  APPROVAL_STATUS = 'PENDING'
          AND  FILE_STATUS    != 'AUTO_REJECTED'
    """)


def reject_all(folder_id: int, header_id: int, user: str, reason: str):
    reason = (reason or "Bulk rejected by SME").replace("'", "''")
    execute(f"""
        UPDATE {DB}.FILE_BATCH_DETAIL
        SET    APPROVAL_STATUS = 'REJECTED',
               UPDATED_DATE   = CURRENT_TIMESTAMP()
        WHERE  FOLDER_ID       = {folder_id}
          AND  APPROVAL_STATUS = 'PENDING'
          AND  FILE_STATUS    != 'AUTO_REJECTED'
    """)


def rename_and_approve(detail_id: int, new_name: str, folder_id: int,
                        header_id: int, user: str):
    safe_name = new_name.replace("'", "''")
    execute(f"""
        UPDATE {DB}.FILE_BATCH_DETAIL
        SET    CURRENT_FILE_NAME = '{safe_name}',
               RENAME_STATUS     = 'COMPLETED',
               RENAMED_BY        = '{user}',
               RENAMED_DATE      = CURRENT_TIMESTAMP(),
               APPROVAL_STATUS   = 'APPROVED',
               APPROVED_BY       = '{user}',
               APPROVED_DATE     = CURRENT_TIMESTAMP(),
               UPDATED_DATE      = CURRENT_TIMESTAMP()
        WHERE  DETAIL_ID = {detail_id}
    """)


def get_activity_log(folder_id: int) -> pd.DataFrame:
    return sql(f"""
        SELECT l.ACTIVITY_ID,
               d.ORIGINAL_FILE_NAME  AS FILE_NAME,
               l.PROCESS_NAME,
               l.ACTIVITY_TYPE,
               l.ACTIVITY_STATUS,
               l.ACTIVITY_MESSAGE,
               l.EXECUTED_BY,
               l.EXECUTED_TIME
        FROM   {DB}.FILE_ACTIVITY_LOG l
        LEFT JOIN {DB}.FILE_BATCH_DETAIL d ON l.DETAIL_ID = d.DETAIL_ID
        WHERE  l.FOLDER_ID = {folder_id}
           OR  l.DETAIL_ID IN (
                   SELECT DETAIL_ID FROM {DB}.FILE_BATCH_DETAIL
                   WHERE  FOLDER_ID = {folder_id}
               )
        ORDER  BY l.EXECUTED_TIME DESC
        LIMIT  100
    """)