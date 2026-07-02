# SFTP Ingestion App

Meduit FACS MDM — SFTP File Ingestion & Curation  

---

## Overview

This repo contains the full SFTP ingestion curation layer:

| Component | Owner | Location |
|---|---|---|
| Snowflake schema (DDL) | Himanshu Kumar | `sql/V001__create_tables.sql` |
| Dev seed data | Himanshu Kumar | `sql/V002__seed_data.sql` |
| Streamlit review UI | Himanshu Kumar | `streamlit_app.py` |
| .NET scanner service | Thameem Ansari | `dotnet/` |

---

## How it works

```
E: Drive (Windows Share)
        ↓
  .NET Scanner (dotnet/)
  Crawls organized\ folder tree
        ↓
  Snowflake — MEDUIT_DEX.SFTP_INGESTION
  FILE_BATCH_HEADER / FOLDER / DETAIL
  FILE_STATUS = DISCOVERED
        ↓
  Streamlit UI (streamlit_app.py)
  SME bulk approves or rejects files
  FILE_STATUS = APPROVED / REJECTED
        ↓
  .NET Promotion Worker (dotnet/)
  Polls APPROVED rows
  PUTs file to Snowflake stage
  FILE_STATUS = INGESTED
```

---

## Snowflake Setup (dev)

**Account:** `qcwzazw-fb12160`  
**Database:** `MEDUIT_DEX`  
**Schema:** `SFTP_INGESTION`  
**Warehouse:** `COMPUTE_WH`

### Step 1 — Create tables
Run `sql/V001__create_tables.sql` in a Snowflake Worksheet.

### Step 2 — Load seed data
Run `sql/V002__seed_data.sql` to populate dev/demo data.

Expected result:
```
FILE_BATCH_HEADER  → 3 rows
FILE_BATCH_FOLDER  → 4 rows
FILE_BATCH_DETAIL  → 9 rows
FILE_ACTIVITY_LOG  → 0 rows
```

### Step 3 — Deploy Streamlit UI

Via Snowflake UI:
1. Snowsight → Streamlit → + Streamlit App
2. Database: `MEDUIT_DEX`, Schema: `SFTP_INGESTION`, Warehouse: `COMPUTE_WH`
3. Paste contents of `streamlit_app.py` → Run

Via Snowflake CLI:
```bash
snow streamlit deploy
```

---

## SQL Versioning Convention

| Version | Description |
|---|---|
| `V001__create_tables.sql` | Initial schema — all 4 tables |
| `V002__seed_data.sql` | Dev seed data simulating .NET scanner output |
| `V003__...` | Next change goes here |

New schema changes always get a new versioned file — never edit an existing one.

---
