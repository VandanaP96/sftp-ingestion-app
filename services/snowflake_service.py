"""
services/snowflake_services.py
All Snowflake read/write operations — schema v2 (3-status model)
Aligned with Thameem's approved query templates.
"""

import pandas as pd
from snowflake.snowpark.context import get_active_session

session = get_active_session()
DB = "MEDUIT_DEX.SFTP_INGESTION"


def sql(query: str) -> pd.DataFrame:
    return session.sql(query).to_pandas()

def execute(query: str):
    session.sql(query).collect()


# ── read ──────────────────────────────────────────────────────────────────────

def get_clients() -> pd.DataFrame:
    """Only return clients that are onboarded in CFG_CLIENT."""
    return sql(f"""
        SELECT h.HEADER_ID, h.CLIENT_CODE, h.CLIENT_NAME
        FROM   {DB}.FILE_BATCH_HEADER h
        INNER JOIN MEDUIT_DEX.DEX.CFG_CLIENT c
               ON h.CLIENT_NAME = c.DISPLAY_NAME
        WHERE  h.ACTIVE_FLAG = 'Y'
        ORDER  BY h.CLIENT_NAME
    """)


def is_fully_loaded(client_name: str) -> bool:
    """
    Returns True if the client has at least one COMPLETE record in CTL_LANDING_LOAD.
    """
    result = sql(f"""
        SELECT COUNT(*) AS CNT
        FROM   MEDUIT_DEX.DEX.CTL_LANDING_LOAD l
        JOIN   MEDUIT_DEX.DEX.CFG_CLIENT c ON l.CLIENT_ID = c.CLIENT_ID
        WHERE  c.DISPLAY_NAME = '{client_name}'
          AND  l.STATUS       = 'COMPLETE'
    """)
    return int(result.iloc[0]["CNT"]) > 0


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
    df = sql(f"""
        SELECT APPROVAL_STATUS AS STATUS, COUNT(*) AS CNT
        FROM   {DB}.FILE_BATCH_DETAIL
        WHERE  FOLDER_ID = {folder_id}
        GROUP  BY APPROVAL_STATUS
    """)
    counts = df.set_index("STATUS")["CNT"].to_dict() if not df.empty else {}
    auto = sql(f"""
        SELECT COUNT(*) AS CNT FROM {DB}.FILE_BATCH_DETAIL
        WHERE  FOLDER_ID = {folder_id} AND FILE_STATUS = 'AUTO_REJECTED'
    """)
    counts["AUTO_REJECTED"] = int(auto.iloc[0]["CNT"]) if not auto.empty else 0
    return counts


# ── folder + header refresh ───────────────────────────────────────────────────

def _refresh_folder(folder_id: int, folder_status: str, header_id: int, user: str):
    execute(f"""
        UPDATE {DB}.FILE_BATCH_FOLDER
        SET    FOLDER_STATUS  = '{folder_status}',
               APPROVED_FILES = (SELECT COUNT(*) FROM {DB}.FILE_BATCH_DETAIL
                                  WHERE FOLDER_ID = {folder_id}
                                    AND APPROVAL_STATUS = 'APPROVED'),
               REJECTED_FILES = (SELECT COUNT(*) FROM {DB}.FILE_BATCH_DETAIL
                                  WHERE FOLDER_ID = {folder_id}
                                    AND APPROVAL_STATUS = 'REJECTED'),
               APPROVED_BY    = '{user}',
               APPROVED_DATE  = CURRENT_TIMESTAMP(),
               UPDATED_BY     = '{user}',
               UPDATED_DATE   = CURRENT_TIMESTAMP()
        WHERE  FOLDER_ID = {folder_id}
    """)
    execute(f"""
        UPDATE {DB}.FILE_BATCH_HEADER
        SET    TOTAL_FOLDER_COUNT = (
                   SELECT COUNT(*) FROM {DB}.FILE_BATCH_FOLDER
                   WHERE  HEADER_ID = {header_id} AND ACTIVE_FLAG = 'Y'),
               TOTAL_FILE_COUNT  = (
                   SELECT COUNT(*) FROM {DB}.FILE_BATCH_DETAIL D
                   INNER JOIN {DB}.FILE_BATCH_FOLDER F ON D.FOLDER_ID = F.FOLDER_ID
                   WHERE  F.HEADER_ID = {header_id}),
               UPDATED_BY        = '{user}',
               UPDATED_DATE      = CURRENT_TIMESTAMP()
        WHERE  HEADER_ID = {header_id}
    """)


# ── approve ───────────────────────────────────────────────────────────────────

def approve_all(folder_id: int, header_id: int, user: str):
    execute(f"""
        UPDATE {DB}.FILE_BATCH_DETAIL
        SET    APPROVAL_STATUS = 'APPROVED',
               FILE_STATUS     = 'APPROVED',
               APPROVED_BY     = '{user}',
               APPROVED_DATE   = CURRENT_TIMESTAMP(),
               UPDATED_BY      = '{user}',
               UPDATED_DATE    = CURRENT_TIMESTAMP()
        WHERE  FOLDER_ID = {folder_id}
          AND  APPROVAL_STATUS NOT IN ('APPROVED', 'REJECTED')
          AND  FILE_STATUS != 'AUTO_REJECTED'
    """)
    _refresh_folder(folder_id, 'APPROVED', header_id, user)


def approve_files(detail_ids: list, folder_id: int, header_id: int, user: str):
    ids = ", ".join(str(i) for i in detail_ids)
    execute(f"""
        UPDATE {DB}.FILE_BATCH_DETAIL
        SET    APPROVAL_STATUS = 'APPROVED',
               FILE_STATUS     = 'APPROVED',
               APPROVED_BY     = '{user}',
               APPROVED_DATE   = CURRENT_TIMESTAMP(),
               UPDATED_BY      = '{user}',
               UPDATED_DATE    = CURRENT_TIMESTAMP()
        WHERE  DETAIL_ID IN ({ids})
          AND  FILE_STATUS != 'AUTO_REJECTED'
    """)
    _refresh_folder(folder_id, 'IN_REVIEW', header_id, user)


# ── reject ────────────────────────────────────────────────────────────────────

def reject_all(folder_id: int, header_id: int, user: str, reason: str):
    reason = (reason or "Bulk rejected by SME").replace("'", "''")
    execute(f"""
        UPDATE {DB}.FILE_BATCH_DETAIL
        SET    APPROVAL_STATUS   = 'REJECTED',
               FILE_STATUS       = 'REJECTED',
               INGESTION_STATUS  = 'NOT_REQUIRED',
               REJECTED_BY       = '{user}',
               REJECTED_DATE     = CURRENT_TIMESTAMP(),
               REJECTION_REASON  = '{reason}',
               UPDATED_BY        = '{user}',
               UPDATED_DATE      = CURRENT_TIMESTAMP()
        WHERE  FOLDER_ID = {folder_id}
          AND  APPROVAL_STATUS NOT IN ('APPROVED', 'REJECTED')
          AND  FILE_STATUS != 'AUTO_REJECTED'
    """)
    _refresh_folder(folder_id, 'REJECTED', header_id, user)


def reject_files(detail_ids: list, folder_id: int, header_id: int,
                 user: str, reason: str):
    ids    = ", ".join(str(i) for i in detail_ids)
    reason = (reason or "Rejected by SME").replace("'", "''")
    execute(f"""
        UPDATE {DB}.FILE_BATCH_DETAIL
        SET    APPROVAL_STATUS   = 'REJECTED',
               FILE_STATUS       = 'REJECTED',
               INGESTION_STATUS  = 'NOT_REQUIRED',
               REJECTED_BY       = '{user}',
               REJECTED_DATE     = CURRENT_TIMESTAMP(),
               REJECTION_REASON  = '{reason}',
               UPDATED_BY        = '{user}',
               UPDATED_DATE      = CURRENT_TIMESTAMP()
        WHERE  DETAIL_ID IN ({ids})
          AND  FILE_STATUS != 'AUTO_REJECTED'
    """)
    _refresh_folder(folder_id, 'IN_REVIEW', header_id, user)


# ── rename & approve ──────────────────────────────────────────────────────────

def rename_and_approve(detail_id: int, new_name: str, folder_id: int,
                       header_id: int, user: str):
    safe_name = new_name.replace("'", "''")
    execute(f"""
        UPDATE {DB}.FILE_BATCH_DETAIL
        SET    CURRENT_FILE_NAME = '{safe_name}',
               APPROVAL_STATUS   = 'APPROVED',
               RENAME_STATUS     = 'READY',
               APPROVED_BY       = '{user}',
               APPROVED_DATE     = CURRENT_TIMESTAMP(),
               UPDATED_BY        = '{user}',
               UPDATED_DATE      = CURRENT_TIMESTAMP()
        WHERE  DETAIL_ID = {detail_id}
    """)
    _refresh_folder(folder_id, 'IN_REVIEW', header_id, user)


# ── activity log ──────────────────────────────────────────────────────────────

def log_action(header_id: int, folder_id: int, detail_id,
               activity_type: str, status: str, message: str, user: str):
    h = str(header_id)
    f = str(folder_id)
    d = "NULL" if detail_id is None else str(detail_id)
    execute(f"""
        INSERT INTO {DB}.FILE_ACTIVITY_LOG
            (HEADER_ID, FOLDER_ID, DETAIL_ID,
             PROCESS_NAME, ACTIVITY_TYPE, ACTIVITY_STATUS,
             ACTIVITY_MESSAGE, EXECUTED_BY)
        VALUES ({h}, {f}, {d},
                'STREAMLIT', '{activity_type}', '{status}',
                $${message}$$, '{user}')
    """)


def get_activity_log(folder_id: int) -> pd.DataFrame:
    return sql(f"""
        SELECT l.ACTIVITY_ID,
               d.ORIGINAL_FILE_NAME AS FILE_NAME,
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