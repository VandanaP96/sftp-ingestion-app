"""
utils/helpers.py
Shared helper functions.
"""

from snowflake.snowpark.context import get_active_session

session = get_active_session()
DB = "MEDUIT_DEX.SFTP_INGESTION"


def me() -> str:
    return session.sql("SELECT CURRENT_USER()").collect()[0][0]


def log_activity(folder_id, detail_id, header_id,
                 activity_type, process_name, status, message, user):
    h  = "NULL" if header_id is None else str(header_id)
    fo = "NULL" if folder_id is None else str(folder_id)
    d  = "NULL" if detail_id is None else str(detail_id)
    session.sql(f"""
        INSERT INTO {DB}.FILE_ACTIVITY_LOG
            (HEADER_ID, FOLDER_ID, DETAIL_ID,
             PROCESS_NAME, ACTIVITY_TYPE, ACTIVITY_STATUS,
             ACTIVITY_MESSAGE, EXECUTED_BY)
        VALUES ({h}, {fo}, {d},
                '{process_name}', '{activity_type}', '{status}',
                $${message}$$, '{user}')
    """).collect()


def log_bulk(folder_id: int, header_id: int, detail_ids: list,
             activity_type: str, process_name: str, message: str, user: str):
    for did in detail_ids:
        log_activity(folder_id, did, header_id,
                     activity_type, process_name, "SUCCESS", message, user)
