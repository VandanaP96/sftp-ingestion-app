"""
services/snowflake_service.py
All Snowflake read/write operations.
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
        SELECT DETAIL_ID, FILE_NAME, FILE_TYPE, FILE_EXTENSION, FILE_STATUS,
               APPROVED_BY, APPROVED_DATE, REJECTED_BY, REJECTED_DATE, REJECTION_REASON
        FROM   {DB}.FILE_BATCH_DETAIL
        WHERE  FOLDER_ID = {folder_id}
        ORDER  BY FILE_NAME
    """)


def get_file_counts(folder_id: int) -> dict:
    df = sql(f"""
        SELECT FILE_STATUS, COUNT(*) AS CNT
        FROM   {DB}.FILE_BATCH_DETAIL
        WHERE  FOLDER_ID = {folder_id}
        GROUP  BY FILE_STATUS
    """)
    return df.set_index("FILE_STATUS")["CNT"].to_dict() if not df.empty else {}


def approve_all(folder_id: int, header_id: int, user: str):
    execute(f"""
        UPDATE {DB}.FILE_BATCH_DETAIL
        SET    FILE_STATUS    = 'APPROVED',
               APPROVED_BY   = '{user}',
               APPROVED_DATE = CURRENT_TIMESTAMP(),
               UPDATED_BY    = '{user}',
               UPDATED_DATE  = CURRENT_TIMESTAMP()
        WHERE  FOLDER_ID   = {folder_id}
          AND  FILE_STATUS NOT IN ('APPROVED', 'INGESTED')
    """)


def reject_all(folder_id: int, header_id: int, user: str, reason: str):
    reason = (reason or "Bulk rejected by SME").replace("'", "''")
    execute(f"""
        UPDATE {DB}.FILE_BATCH_DETAIL
        SET    FILE_STATUS       = 'REJECTED',
               REJECTED_BY      = '{user}',
               REJECTED_DATE    = CURRENT_TIMESTAMP(),
               REJECTION_REASON = '{reason}',
               UPDATED_BY       = '{user}',
               UPDATED_DATE     = CURRENT_TIMESTAMP()
        WHERE  FOLDER_ID   = {folder_id}
          AND  FILE_STATUS NOT IN ('APPROVED', 'INGESTED')
    """)


def get_activity_log(folder_id: int) -> pd.DataFrame:
    return sql(f"""
        SELECT l.ACTIVITY_ID, d.FILE_NAME, l.ACTIVITY_TYPE,
               l.ACTIVITY_STATUS, l.ACTIVITY_MESSAGE,
               l.EXECUTED_BY, l.EXECUTED_TIME
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