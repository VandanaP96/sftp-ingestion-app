"""
streamlit_app.py
SFTP Ingestion - File Review Screen
Streamlit in Snowflake | Matches Rule Studio design language
Database : MEDUIT_DEX | Schema : SFTP_INGESTION
"""

import streamlit as st
from services.snowflake_service import (
    get_clients, get_folders, get_files,
    get_file_counts, approve_all, reject_all, get_activity_log
)
from utils.helpers import me, log_bulk


# ── page config ───────────────────────────────────────────────────────────────

st.set_page_config(page_title="SFTP File Review", page_icon="📂", layout="wide")

st.markdown("""
<style>
    [data-testid="stSidebar"] { background-color: #1B2A4A !important; }
    [data-testid="stSidebar"] * { color: #FFFFFF !important; }
    [data-testid="stSidebar"] .stSelectbox label,
    [data-testid="stSidebar"] p,
    [data-testid="stSidebar"] span { color: #CBD5E1 !important; font-size: 0.82rem; }
    [data-testid="stSidebar"] hr { border-color: #2E4270 !important; }

    .app-brand { font-size:1.05rem; font-weight:700; color:#FFFFFF; letter-spacing:0.5px; margin-bottom:4px; }
    .app-sub   { font-size:0.75rem; color:#94A3B8; margin-bottom:1.5rem; }

    .page-title { font-size:1.4rem; font-weight:700; color:#1B2A4A;
                  border-bottom:2px solid #0097A7; padding-bottom:6px; margin-bottom:4px; }
    .page-desc  { font-size:0.82rem; color:#64748B; margin-bottom:1rem; }

    .chip      { display:inline-block; padding:3px 12px; border-radius:999px;
                 font-size:0.78rem; font-weight:600; margin-right:8px; }
    .chip-disc { background:#EFF6FF; color:#1D4ED8; border:1px solid #BFDBFE; }
    .chip-appr { background:#ECFDF5; color:#065F46; border:1px solid #6EE7B7; }
    .chip-rej  { background:#FEF2F2; color:#991B1B; border:1px solid #FECACA; }
    .chip-ing  { background:#F8FAFC; color:#475569; border:1px solid #CBD5E1; }

    .tbl-header { font-size:0.75rem; font-weight:700; color:#0097A7;
                  text-transform:uppercase; letter-spacing:0.5px;
                  padding:8px 0 6px 0; border-bottom:1px solid #E2E8F0; margin-bottom:4px; }

    .action-label { font-size:0.78rem; color:#64748B; font-weight:600;
                    margin-bottom:6px; text-transform:uppercase; letter-spacing:0.4px; }

    .path-pill { background:#F1F5F9; border:1px solid #CBD5E1; border-radius:6px;
                 padding:6px 12px; font-size:0.78rem; color:#475569;
                 font-family:monospace; margin-bottom:1rem; word-break:break-all; }
</style>
""", unsafe_allow_html=True)


# ── sidebar ───────────────────────────────────────────────────────────────────

user = me()

with st.sidebar:
    st.markdown('<div class="app-brand">📂 Meduit MDM</div>', unsafe_allow_html=True)
    st.markdown('<div class="app-sub">SFTP Ingestion Review</div>', unsafe_allow_html=True)
    st.markdown("---")

    st.markdown("**Client**")
    clients = get_clients()
    if clients.empty:
        st.warning("No active clients.")
        st.stop()

    client_map      = {r["CLIENT_NAME"]: r for _, r in clients.iterrows()}
    selected_name   = st.selectbox("", list(client_map.keys()), label_visibility="collapsed")
    selected_client = client_map[selected_name]
    header_id       = int(selected_client["HEADER_ID"])

    st.markdown("---")

    st.markdown("**Year-Month**")
    folders = get_folders(header_id)
    if folders.empty:
        st.info("No folders for this client.")
        st.stop()

    folder_map      = {r["YEAR_MONTH"]: r for _, r in folders.iterrows()}
    selected_ym     = st.selectbox("", list(folder_map.keys()), label_visibility="collapsed")
    selected_folder = folder_map[selected_ym]
    folder_id       = int(selected_folder["FOLDER_ID"])

    st.markdown("---")
    if st.button("🔄 Refresh", use_container_width=True):
        st.rerun()
    st.markdown(
        f"<div style='font-size:0.72rem;color:#94A3B8;margin-top:8px;'>Logged in as {user}</div>",
        unsafe_allow_html=True
    )


# ── main ──────────────────────────────────────────────────────────────────────

st.markdown('<div class="page-title">File Review</div>', unsafe_allow_html=True)
st.markdown(
    f'<div class="page-desc">'
    f'{selected_client["CLIENT_NAME"]} &nbsp;·&nbsp; {selected_ym} &nbsp;·&nbsp; '
    f'Scanned: {selected_folder["SCANNED_DATE"]}'
    f'</div>',
    unsafe_allow_html=True,
)

st.markdown(
    f'<div class="path-pill">📁 {selected_folder["FOLDER_PATH"]}</div>',
    unsafe_allow_html=True
)

# ── stats chips ───────────────────────────────────────────────────────────────

counts = get_file_counts(folder_id)
st.markdown(
    f'<span class="chip chip-disc">🔵 {counts.get("DISCOVERED", 0)} Pending</span>'
    f'<span class="chip chip-appr">✅ {counts.get("APPROVED", 0)} Approved</span>'
    f'<span class="chip chip-rej">❌ {counts.get("REJECTED", 0)} Rejected</span>'
    f'<span class="chip chip-ing">📦 {counts.get("INGESTED", 0)} Ingested</span>',
    unsafe_allow_html=True
)

st.markdown("<br>", unsafe_allow_html=True)

# ── file table ────────────────────────────────────────────────────────────────

files = get_files(folder_id)

st.markdown(
    f'<div class="tbl-header">Files in this folder — {len(files)} total</div>',
    unsafe_allow_html=True
)

if files.empty:
    st.info("No files found for this client and folder.")
    st.stop()

st.dataframe(
    files[["FILE_NAME", "FILE_TYPE", "FILE_EXTENSION", "FILE_STATUS",
           "APPROVED_BY", "APPROVED_DATE", "REJECTED_BY", "REJECTED_DATE", "REJECTION_REASON"]],
    column_config={
        "FILE_NAME":        st.column_config.TextColumn("File Name",     width="large"),
        "FILE_TYPE":        st.column_config.TextColumn("Type",          width="medium"),
        "FILE_EXTENSION":   st.column_config.TextColumn("Ext",           width="small"),
        "FILE_STATUS":      st.column_config.TextColumn("Status",        width="medium"),
        "APPROVED_BY":      st.column_config.TextColumn("Approved By",   width="medium"),
        "APPROVED_DATE":    st.column_config.DatetimeColumn("Approved At", width="medium"),
        "REJECTED_BY":      st.column_config.TextColumn("Rejected By",   width="medium"),
        "REJECTED_DATE":    st.column_config.DatetimeColumn("Rejected At", width="medium"),
        "REJECTION_REASON": st.column_config.TextColumn("Reason",        width="medium"),
    },
    use_container_width=True,
    hide_index=True,
)

# ── bulk actions ──────────────────────────────────────────────────────────────

st.markdown("<br>", unsafe_allow_html=True)
st.markdown('<div class="action-label">Bulk Action — applies to all files in this folder</div>',
            unsafe_allow_html=True)

action = st.radio(
    "",
    options=["— Select action —", "✅  Approve All", "❌  Reject All"],
    horizontal=True,
    index=0,
    key=f"action_{folder_id}",
)

if action == "✅  Approve All":
    pending = counts.get("DISCOVERED", 0)
    if pending == 0:
        st.warning("No pending files to approve in this folder.")
    else:
        detail_ids = files["DETAIL_ID"].tolist()
        st.info(f"This will approve all **{pending}** pending file(s) in `{selected_ym}`.")
        if st.button("Confirm Approve All", type="primary"):
            approve_all(folder_id, header_id, user)
            log_bulk(folder_id, header_id, detail_ids,
                     "FILE_APPROVED", "Bulk approved from SFTP review screen.", user)
            st.success(f"✅ Approved {pending} file(s).")
            st.rerun()

elif action == "❌  Reject All":
    pending = counts.get("DISCOVERED", 0)
    if pending == 0:
        st.warning("No pending files to reject in this folder.")
    else:
        detail_ids = files["DETAIL_ID"].tolist()
        reason = st.text_input(
            "Rejection reason (optional)",
            placeholder="e.g. Wrong period, duplicate batch, processed output…"
        )
        st.info(f"This will reject all **{pending}** pending file(s) in `{selected_ym}`.")
        if st.button("Confirm Reject All", type="secondary"):
            reject_all(folder_id, header_id, user, reason)
            log_bulk(folder_id, header_id, detail_ids,
                     "FILE_REJECTED", f"Bulk rejected. Reason: {reason or 'N/A'}", user)
            st.success(f"❌ Rejected {pending} file(s).")
            st.rerun()

# ── activity log ──────────────────────────────────────────────────────────────

st.markdown("<br>", unsafe_allow_html=True)
with st.expander("📋 Activity Log"):
    log_df = get_activity_log(folder_id)
    if log_df.empty:
        st.caption("No activity yet.")
    else:
        st.dataframe(log_df, use_container_width=True, hide_index=True)