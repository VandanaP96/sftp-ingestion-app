import streamlit as st
import pandas as pd
from services.snowflake_service import (
    get_clients, get_folders, get_files, get_approval_counts,
    approve_all, approve_files, reject_all, reject_files,
    rename_and_approve, log_action, get_activity_log, is_fully_loaded
)
from utils.helpers import me


# ── page config ───────────────────────────────────────────────────────────────

st.set_page_config(page_title="SFTP File Review", page_icon="📂", layout="wide")

st.markdown("""
<style>
    [data-testid="stSidebar"] { background-color: #1B2A4A !important; }
    [data-testid="stSidebar"] label,
    [data-testid="stSidebar"] p,
    [data-testid="stSidebar"] span,
    [data-testid="stSidebar"] .stMarkdown { color: #FFFFFF !important; }
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

    .tbl-header   { font-size:0.75rem; font-weight:700; color:#0097A7;
                    text-transform:uppercase; letter-spacing:0.5px;
                    padding:8px 0 6px 0; border-bottom:1px solid #E2E8F0; margin-bottom:4px; }
    .action-label { font-size:0.78rem; color:#64748B; font-weight:600;
                    text-transform:uppercase; letter-spacing:0.4px; margin-bottom:6px; }
    .path-pill    { background:#F1F5F9; border:1px solid #CBD5E1; border-radius:6px;
                    padding:6px 12px; font-size:0.78rem; color:#475569;
                    font-family:monospace; margin-bottom:1rem; word-break:break-all; }
    .rename-box   { background:#FFFBEB; border:1px solid #FDE68A; border-radius:8px;
                    padding:12px 16px; margin-bottom:8px; }
    .rename-lbl   { font-size:0.78rem; color:#92400E; font-weight:600; margin-bottom:4px; }
    .partial-note { font-size:0.78rem; color:#6B7280; font-style:italic; margin-top:4px; }
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

# ── load files ────────────────────────────────────────────────────────────────

all_files = get_files(folder_id)
if all_files.empty:
    st.info("No files found for this client and folder.")
    st.stop()

# ── onboarding load check ─────────────────────────────────────────────────────
fully_loaded = is_fully_loaded(selected_client["CLIENT_NAME"])

if not fully_loaded:
    st.warning(
        f"⚠️ **{selected_client['CLIENT_NAME']}** is onboarded but not fully loaded. "
        f"Showing top 1 file only until loading is complete."
    )
    all_files = all_files.head(1)

normal_files  = all_files[all_files["APPROVAL_STATUS"] == "PENDING"].reset_index(drop=True)
rename_files  = all_files[all_files["APPROVAL_STATUS"] == "RENAME_REQUIRED"].reset_index(drop=True)
auto_rej      = all_files[all_files["FILE_STATUS"]     == "AUTO_REJECTED"].reset_index(drop=True)
done_files    = all_files[
    all_files["APPROVAL_STATUS"].isin(["APPROVED", "REJECTED"]) &
    (all_files["FILE_STATUS"] != "AUTO_REJECTED")
].reset_index(drop=True)

total_pending = len(normal_files)


# ── Section 1: Pending Review ─────────────────────────────────────────────────

st.markdown(
    f'<div class="tbl-header">Pending Review — {total_pending} files</div>',
    unsafe_allow_html=True
)

if normal_files.empty:
    st.caption("No files pending review.")
else:
    st.markdown('<div class="action-label">Bulk Action</div>', unsafe_allow_html=True)
    action = st.radio(
        "",
        options=["— Select action —", "✅  Approve", "❌  Reject"],
        horizontal=True,
        index=0,
        key=f"action_{folder_id}",
    )

    display = normal_files[["DETAIL_ID", "CURRENT_FILE_NAME", "FILE_TYPE",
                             "FILE_SIZE_KB", "VALID_DATE_FLAG",
                             "VALIDATION_MESSAGE"]].copy()

    if action in ["✅  Approve", "❌  Reject"]:
        display.insert(0, "Select", True)
    else:
        display.insert(0, "Select", False)

    edited = st.data_editor(
        display,
        column_config={
            "Select":            st.column_config.CheckboxColumn("✔", width="small"),
            "DETAIL_ID":         st.column_config.NumberColumn("ID", width="small"),
            "CURRENT_FILE_NAME": st.column_config.TextColumn("File Name", width="large"),
            "FILE_TYPE":         st.column_config.TextColumn("Type", width="medium"),
            "FILE_SIZE_KB":      st.column_config.NumberColumn("Size (KB)", width="small"),
            "VALID_DATE_FLAG":   st.column_config.TextColumn("Valid Date", width="small"),
            "VALIDATION_MESSAGE":st.column_config.TextColumn("Validation", width="medium"),
        },
        use_container_width=True,
        hide_index=True,
        num_rows="fixed",
        key=f"tbl_{folder_id}",
    )

    selected_rows = edited[edited["Select"] == True]
    selected_ids  = normal_files.loc[selected_rows.index, "DETAIL_ID"].tolist()
    is_full_bulk  = len(selected_ids) == total_pending

    if selected_ids and action != "— Select action —":
        if not is_full_bulk:
            st.markdown(
                f'<div class="partial-note">'
                f'{len(selected_ids)} of {total_pending} files selected — partial action.</div>',
                unsafe_allow_html=True
            )

    # ── THE FIX: only use folder-level bulk query when fully loaded AND all selected ──
    if action == "✅  Approve" and selected_ids:
        if st.button("Confirm Approve", type="primary"):
            if is_full_bulk and fully_loaded:
                approve_all(folder_id, header_id, user)
                log_action(header_id, folder_id, None,
                           "APPROVAL", "SUCCESS", "Folder Approved", user)
            else:
                approve_files(selected_ids, folder_id, header_id, user)
                for did in selected_ids:
                    log_action(header_id, folder_id, did,
                               "APPROVAL", "SUCCESS", "File Approved", user)
            st.success(f"✅ Approved {len(selected_ids)} file(s).")
            st.rerun()

    elif action == "❌  Reject" and selected_ids:
        reason = st.text_input(
            "Rejection reason (optional)",
            placeholder="e.g. Wrong period, duplicate batch…",
            key="reject_reason"
        )
        if st.button("Confirm Reject", type="secondary"):
            if is_full_bulk and fully_loaded:
                reject_all(folder_id, header_id, user, reason)
                log_action(header_id, folder_id, None,
                           "REJECTION", "SUCCESS", "Folder Rejected", user)
            else:
                reject_files(selected_ids, folder_id, header_id, user, reason)
                for did in selected_ids:
                    log_action(header_id, folder_id, did,
                               "REJECTION", "SUCCESS",
                               f"File Rejected. Reason: {reason or 'N/A'}", user)
            st.success(f"❌ Rejected {len(selected_ids)} file(s).")
            st.rerun()


# ── Section 2: Rename Required ────────────────────────────────────────────────

if not rename_files.empty:
    st.markdown("<br>", unsafe_allow_html=True)
    st.markdown(
        f'<div class="tbl-header">⚠️ Rename Required — {len(rename_files)} files</div>',
        unsafe_allow_html=True
    )
    st.caption("Uncheck files you do not want to rename. Edit the corrected filename, then click Submit.")

    rename_display = rename_files[["DETAIL_ID", "ORIGINAL_FILE_NAME",
                                    "CURRENT_FILE_NAME", "VALIDATION_MESSAGE"]].copy()
    rename_display.insert(0, "Select", True)
    rename_display = rename_display.rename(columns={"CURRENT_FILE_NAME": "CORRECTED_FILE_NAME"})

    edited_renames = st.data_editor(
        rename_display,
        column_config={
            "Select":               st.column_config.CheckboxColumn("✔", width="small"),
            "DETAIL_ID":            st.column_config.NumberColumn("ID", width="small"),
            "ORIGINAL_FILE_NAME":   st.column_config.TextColumn("Original Filename",     width="large",  disabled=True),
            "CORRECTED_FILE_NAME":  st.column_config.TextColumn("Corrected Filename ✏️",  width="large"),
            "VALIDATION_MESSAGE":   st.column_config.TextColumn("Reason",                width="medium", disabled=True),
        },
        use_container_width=True,
        hide_index=True,
        num_rows="fixed",
        key=f"rename_tbl_{folder_id}",
    )

    selected_renames = edited_renames[edited_renames["Select"] == True]
    st.caption(f"{len(selected_renames)} of {len(rename_files)} file(s) selected for rename & approve.")

    if st.button("✅ Submit Selected Renames", type="primary",
                 disabled=len(selected_renames) == 0):
        submitted = 0
        for i, row in selected_renames.iterrows():
            new_name  = str(row["CORRECTED_FILE_NAME"]).strip()
            detail_id = int(rename_files.loc[i, "DETAIL_ID"])
            if new_name:
                rename_and_approve(detail_id, new_name, folder_id, header_id, user)
                log_action(header_id, folder_id, detail_id,
                           "RENAME_REQUEST", "SUCCESS",
                           f"Rename Approved. New name: {new_name}", user)
                submitted += 1
        if submitted:
            st.success(f"✅ {submitted} file(s) renamed and approved.")
            st.rerun()


# ── Section 3: Auto-Rejected ──────────────────────────────────────────────────

if not auto_rej.empty:
    st.markdown("<br>", unsafe_allow_html=True)
    with st.expander(f"🚫 Auto-Rejected by Scanner — {len(auto_rej)} files (read only)"):
        st.caption("Automatically rejected by the .NET scanner due to invalid date. No action available.")
        st.dataframe(
            auto_rej[["DETAIL_ID", "ORIGINAL_FILE_NAME", "FILE_TYPE",
                       "VALID_DATE_FLAG", "VALIDATION_MESSAGE",
                       "FILE_STATUS", "QUARANTINE_PATH"]],
            use_container_width=True,
            hide_index=True,
        )


# ── Section 4: Already Actioned ───────────────────────────────────────────────

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