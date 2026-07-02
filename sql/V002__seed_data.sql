-- ============================================================
-- V002 : Seed Data (Dev / Demo)
-- Purpose : Simulates what the .NET scanner writes after
--           crawling the E: drive organized folder structure.
--           Replace with real data once .NET service is live.
-- Date    : 2026-07-02
-- ============================================================

USE DATABASE MEDUIT_DEX;
USE SCHEMA   SFTP_INGESTION;


-- ── FILE_BATCH_HEADER (one row per client) ────────────────────────────────────

INSERT INTO FILE_BATCH_HEADER (CLIENT_CODE, CLIENT_NAME, SOURCE_SYSTEM, ROOT_FOLDER, ACTIVE_FLAG, CREATED_BY)
VALUES
('MCD1', 'HEMPHILL COUNTY (EMS) - MO0131', 'MEDUIT', 'E:\Meduit\Normalized\organized\MCD1\HEMPHILL COUNTY (EMS) - MO0131', 'Y', 'SYSTEM'),
('MCD1', 'Citizens Med Ctr',                'MEDUIT', 'E:\Meduit\Normalized\organized\MCD1\Citizens Med Ctr',                'Y', 'SYSTEM'),
('MCD1', 'Dayton Childrens',                'MEDUIT', 'E:\Meduit\Normalized\organized\MCD1\Dayton Childrens',                'Y', 'SYSTEM');


-- ── FILE_BATCH_FOLDER (one row per year-month subfolder) ──────────────────────

INSERT INTO FILE_BATCH_FOLDER (HEADER_ID, YEAR_MONTH, FOLDER_NAME, FOLDER_PATH, SCANNED_DATE, CREATED_BY)
VALUES
(1, '2026-01', '2026-01', 'E:\Meduit\Normalized\organized\MCD1\HEMPHILL COUNTY (EMS) - MO0131\2026-01', CURRENT_TIMESTAMP(), 'SYSTEM'),
(1, '2026-02', '2026-02', 'E:\Meduit\Normalized\organized\MCD1\HEMPHILL COUNTY (EMS) - MO0131\2026-02', CURRENT_TIMESTAMP(), 'SYSTEM'),
(2, '2026-01', '2026-01', 'E:\Meduit\Normalized\organized\MCD1\Citizens Med Ctr\2026-01',                CURRENT_TIMESTAMP(), 'SYSTEM'),
(3, '2026-01', '2026-01', 'E:\Meduit\Normalized\organized\MCD1\Dayton Childrens\2026-01',                CURRENT_TIMESTAMP(), 'SYSTEM');


-- ── FILE_BATCH_DETAIL (one row per discovered file) ───────────────────────────

INSERT INTO FILE_BATCH_DETAIL (FOLDER_ID, FILE_NAME, FILE_TYPE, FILE_EXTENSION, FILE_PATH, FILE_STATUS)
VALUES
-- HEMPHILL 2026-01
(1, 'HEMPHILL 010126 IMPORT.txt',       'IMPORT',        'txt', 'E:\Meduit\Normalized\organized\MCD1\HEMPHILL COUNTY (EMS) - MO0131\2026-01\HEMPHILL 010126 IMPORT.txt',       'DISCOVERED'),
(1, 'HEMPHILL 010126 BYPASSED.txt',     'BYPASS',        'txt', 'E:\Meduit\Normalized\organized\MCD1\HEMPHILL COUNTY (EMS) - MO0131\2026-01\HEMPHILL 010126 BYPASSED.txt',     'DISCOVERED'),
(1, 'HEMPHILL 010126 RECALL.txt',       'RECALL IMPORT', 'txt', 'E:\Meduit\Normalized\organized\MCD1\HEMPHILL COUNTY (EMS) - MO0131\2026-01\HEMPHILL 010126 RECALL.txt',       'DISCOVERED'),
-- HEMPHILL 2026-02
(2, 'HEMPHILL 020126 IMPORT.txt',       'IMPORT',        'txt', 'E:\Meduit\Normalized\organized\MCD1\HEMPHILL COUNTY (EMS) - MO0131\2026-02\HEMPHILL 020126 IMPORT.txt',       'DISCOVERED'),
-- Citizens 2026-01
(3, 'CITIZENS MED 010126 IMPORT.txt',   'IMPORT',        'txt', 'E:\Meduit\Normalized\organized\MCD1\Citizens Med Ctr\2026-01\CITIZENS MED 010126 IMPORT.txt',                  'DISCOVERED'),
(3, 'CITIZENS MED 010126 BYPASSED.txt', 'BYPASS',        'txt', 'E:\Meduit\Normalized\organized\MCD1\Citizens Med Ctr\2026-01\CITIZENS MED 010126 BYPASSED.txt',                'DISCOVERED'),
-- Dayton 2026-01
(4, 'DAYTON CHILDREN 010226.txt',       'IMPORT',        'txt', 'E:\Meduit\Normalized\organized\MCD1\Dayton Childrens\2026-01\DAYTON CHILDREN 010226.txt',                      'DISCOVERED'),
(4, 'DAYTON CHILDREN 010226 ACK.txt',   'ACK',           'txt', 'E:\Meduit\Normalized\organized\MCD1\Dayton Childrens\2026-01\DAYTON CHILDREN 010226 ACK.txt',                  'DISCOVERED'),
(4, 'DAYTON CHILDREN LINE REVIEW.txt',  'LINE REVIEW',   'txt', 'E:\Meduit\Normalized\organized\MCD1\Dayton Childrens\2026-01\DAYTON CHILDREN LINE REVIEW.txt',                 'DISCOVERED');


-- ── Verify ────────────────────────────────────────────────────────────────────

SELECT 'FILE_BATCH_HEADER' AS TBL, COUNT(*) AS ROW_COUNT FROM FILE_BATCH_HEADER UNION ALL
SELECT 'FILE_BATCH_FOLDER',        COUNT(*)               FROM FILE_BATCH_FOLDER UNION ALL
SELECT 'FILE_BATCH_DETAIL',        COUNT(*)               FROM FILE_BATCH_DETAIL UNION ALL
SELECT 'FILE_ACTIVITY_LOG',        COUNT(*)               FROM FILE_ACTIVITY_LOG;