"""
SFTP Ingestion - File Review Screen v2
Streamlit in Snowflake | Thameem's schema v2 (3-status model)
Database : MEDUIT_DEX | Schema : SFTP_INGESTION
"""

import streamlit as st
from services.snowflake_service import (
    get_clients, get_folders, get_files,
    get_approval_counts, approve_all, reject_all,
    rename_and_approve, get_activity_log
)
from utils.helpers import me, log_bulk


# ── page config ───────────────────────────────────────────────────────────────

st.set_page_config(page_title="SFTP File Review", page_icon="📂", layout="wide")

st.markdown("""
<style>
    [data-testid="stSidebar"] { background-color: #1B2A4A !important; }
    [data-testid="stSidebar"] label,
    [data-testid="stSidebar"] p,
    [data-testid="stSidebar"] span,
    [data-testid="stSidebar"] .stMarkdown,
    [data-testid="stSidebar"] .stButton button { color: #FFFFFF !important; }
    [data-testid="stSidebar"] .stSelectbox label,
    [data-testid="stSidebar"] p,
    [data-testid="stSidebar"] span { color: #CBD5E1 !important; font-size:0.82rem; }
    [data-testid="stSidebar"] hr { border-color: #2E4270 !important; }

    .app-brand  { font-size:1.05rem; font-weight:700; color:#FFFFFF; letter-spacing:0.5px; margin-bottom:4px; }
    .app-sub    { font-size:0.75rem; color:#94A3B8; margin-bottom:1.5rem; }
    .page-title { font-size:1.4rem; font-weight:700; color:#1B2A4A;
                  border-bottom:2px solid #0097A7; padding-bottom:6px; margin-bottom:4px; }
    .page-desc  { font-size:0.82rem; color:#64748B; margin-bottom:1rem; }

    .chip      { display:inline-block; padding:3px 12px; border-radius:999px;
                 font-size:0.78rem; font-weight:600; margin-right:8px; margin-bottom:8px; }
    .chip-pend { background:#EFF6FF; color:#1D4ED8; border:1px solid #BFDBFE; }
    .chip-appr { background:#ECFDF5; color:#065F46; border:1px solid #6EE7B7; }
    .chip-rej  { background:#FEF2F2; color:#991B1B; border:1px solid #FECACA; }
    .chip-auto { background:#FFF7ED; color:#9A3412; border:1px solid #FDBA74; }
    .chip-ren  { background:#FEFCE8; color:#854D0E; border:1px solid #FDE047; }

    .tbl-header { font-size:0.75rem; font-weight:700; color:#0097A7;
                  text-transform:uppercase; letter-spacing:0.5px;
                  padding:8px 0 6px 0; border-bottom:1px solid #E2E8F0; margin-bottom:4px; }
    .action-label { font-size:0.78rem; color:#64748B; font-weight:600;
                    text-transform:uppercase; letter-spacing:0.4px; margin-bottom:6px; }
    .path-pill  { background:#F1F5F9; border:1px solid #CBD5E1; border-radius:6px;
                  padding:6px 12px; font-size:0.78rem; color:#475569;
                  font-family:monospace; margin-bottom:1rem; word-break:break-all; }
    .rename-box { background:#FFFBEB; border:1px solid #FDE68A; border-radius:8px;
                  padding:12px 16px; margin-bottom:8px; }
    .rename-lbl { font-size:0.78rem; color:#92400E; font-weight:600; margin-bottom:4px; }
    .auto-rej-box { background:#FFF7ED; border:1px solid #FDBA74; border-radius:8px;
                    padding:12px 16px; margin-bottom:8px; }
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


# ── page header ───────────────────────────────────────────────────────────────

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

counts = get_approval_counts(folder_id)
st.markdown(
    f'<span class="chip chip-pend">🔵 {counts.get("PENDING", 0)} Pending</span>'
    f'<span class="chip chip-appr">✅ {counts.get("APPROVED", 0)} Approved</span>'
    f'<span class="chip chip-rej">❌ {counts.get("REJECTED", 0)} Rejected</span>'
    f'<span class="chip chip-ren">⚠️ {counts.get("RENAME_REQUIRED", 0)} Rename Required</span>'
    f'<span class="chip chip-auto">🚫 {counts.get("AUTO_REJECTED", 0)} Auto-Rejected</span>',
    unsafe_allow_html=True
)

st.markdown("<br>", unsafe_allow_html=True)

# ── load all files ────────────────────────────────────────────────────────────

all_files = get_files(folder_id)

if all_files.empty:
    st.info("No files found for this client and folder.")
    st.stop()

# Split into groups
normal_files      = all_files[all_files["APPROVAL_STATUS"] == "PENDING"]
rename_files      = all_files[all_files["APPROVAL_STATUS"] == "RENAME_REQUIRED"]
auto_reject_files = all_files[all_files["FILE_STATUS"]     == "AUTO_REJECTED"]
done_files        = all_files[all_files["APPROVAL_STATUS"].isin(["APPROVED", "REJECTED"]) &
                               (all_files["FILE_STATUS"] != "AUTO_REJECTED")]


# ── Section 1: Files pending review ───────────────────────────────────────────

st.markdown(
    f'<div class="tbl-header">Pending Review — {len(normal_files)} files</div>',
    unsafe_allow_html=True
)

if normal_files.empty:
    st.caption("No files pending review.")
else:
    st.dataframe(
        normal_files[["DETAIL_ID", "CURRENT_FILE_NAME", "FILE_TYPE",
                      "FILE_SIZE_KB", "VALID_DATE_FLAG", "VALIDATION_MESSAGE",
                      "FILE_STATUS", "APPROVAL_STATUS"]],
        use_container_width=True,
        hide_index=True,
    )

    # Bulk actions for normal pending files
    st.markdown('<div class="action-label">Bulk Action — applies to all pending files</div>',
                unsafe_allow_html=True)

    action = st.radio(
        "",
        options=["— Select action —", "✅  Approve All", "❌  Reject All"],
        horizontal=True,
        index=0,
        key=f"action_{folder_id}",
    )

    pending_ids = normal_files["DETAIL_ID"].tolist()

    if action == "✅  Approve All":
        st.info(f"This will approve all **{len(pending_ids)}** pending file(s) in `{selected_ym}`.")
        if st.button("Confirm Approve All", type="primary"):
            approve_all(folder_id, header_id, user)
            log_bulk(folder_id, header_id, pending_ids,
                     "FILE_APPROVED", "StreamlitReview",
                     "Bulk approved from SFTP review screen.", user)
            st.success(f"✅ Approved {len(pending_ids)} file(s).")
            st.rerun()

    elif action == "❌  Reject All":
        reason = st.text_input(
            "Rejection reason (optional)",
            placeholder="e.g. Wrong period, duplicate batch…"
        )
        st.info(f"This will reject all **{len(pending_ids)}** pending file(s) in `{selected_ym}`.")
        if st.button("Confirm Reject All", type="secondary"):
            reject_all(folder_id, header_id, user, reason)
            log_bulk(folder_id, header_id, pending_ids,
                     "FILE_REJECTED", "StreamlitReview",
                     f"Bulk rejected. Reason: {reason or 'N/A'}", user)
            st.success(f"❌ Rejected {len(pending_ids)} file(s).")
            st.rerun()


# ── Section 2: Rename Required ────────────────────────────────────────────────

if not rename_files.empty:
    st.markdown("<br>", unsafe_allow_html=True)
    st.markdown(
        f'<div class="tbl-header">⚠️ Rename Required — {len(rename_files)} files</div>',
        unsafe_allow_html=True
    )
    st.caption("These files need a corrected filename before they can be approved. "
               "Enter the new name and click **Rename & Approve**.")

    for _, row in rename_files.iterrows():
        detail_id = int(row["DETAIL_ID"])
        st.markdown(
            f'<div class="rename-box">'
            f'<div class="rename-lbl">⚠️ Original: {row["ORIGINAL_FILE_NAME"]}</div>',
            unsafe_allow_html=True
        )
        new_name = st.text_input(
            "Corrected filename",
            value=row["CURRENT_FILE_NAME"],
            key=f"rename_{detail_id}",
        )
        col1, col2 = st.columns([2, 8])
        with col1:
            if st.button("Rename & Approve", key=f"btn_rename_{detail_id}", type="primary"):
                if not new_name.strip():
                    st.error("Please enter a valid filename.")
                else:
                    rename_and_approve(detail_id, new_name.strip(), folder_id, header_id, user)
                    st.success(f"✅ Renamed and approved: `{new_name.strip()}`")
                    st.rerun()
        st.markdown("</div>", unsafe_allow_html=True)


# ── Section 3: Auto-Rejected (read only) ──────────────────────────────────────

if not auto_reject_files.empty:
    st.markdown("<br>", unsafe_allow_html=True)
    with st.expander(f"🚫 Auto-Rejected by Scanner — {len(auto_reject_files)} files (read only)"):
        st.caption("These files were automatically rejected by the .NET scanner due to invalid date. "
                   "No action can be taken.")
        st.dataframe(
            auto_reject_files[["DETAIL_ID", "ORIGINAL_FILE_NAME", "FILE_TYPE",
                                "VALID_DATE_FLAG", "VALIDATION_MESSAGE",
                                "FILE_STATUS", "QUARANTINE_PATH"]],
            use_container_width=True,
            hide_index=True,
        )


# ── Section 4: Already actioned ───────────────────────────────────────────────

if not done_files.empty:
    st.markdown("<br>", unsafe_allow_html=True)
    with st.expander(f"📋 Already Actioned — {len(done_files)} files"):
        st.dataframe(
            done_files[["DETAIL_ID", "CURRENT_FILE_NAME", "FILE_TYPE",
                         "APPROVAL_STATUS", "APPROVED_BY", "APPROVED_DATE",
                         "RENAME_STATUS", "RENAMED_BY", "RENAMED_DATE"]],
            use_container_width=True,
            hide_index=True,
        )


# ── Activity log ──────────────────────────────────────────────────────────────

st.markdown("<br>", unsafe_allow_html=True)
with st.expander("📋 Activity Log"):
    log_df = get_activity_log(folder_id)
    if log_df.empty:
        st.caption("No activity yet.")
    else:
        st.dataframe(log_df, use_container_width=True, hide_index=True)
