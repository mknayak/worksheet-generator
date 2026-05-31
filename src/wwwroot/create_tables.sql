-- =============================================================================
-- Worksheet Generator — PostgreSQL schema
-- Run this once against your PostgreSQL database before first launch.
-- All statements are idempotent (IF NOT EXISTS), so re-running is safe.
-- =============================================================================

-- -----------------------------------------------------------------------------
-- Profiles
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS "Profiles" (
    "Id"          TEXT        NOT NULL,
    "Name"        TEXT        NOT NULL DEFAULT '',
    "ClassName"   TEXT        NOT NULL DEFAULT '',
    "Grade"       TEXT        NOT NULL DEFAULT '',
    "School"      TEXT        NOT NULL DEFAULT '',
    "TeacherName" TEXT        NOT NULL DEFAULT '',
    "CreatedAt"   TIMESTAMP   NOT NULL DEFAULT NOW(),
    "UpdatedAt"   TIMESTAMP   NOT NULL DEFAULT NOW(),
    CONSTRAINT "PK_Profiles" PRIMARY KEY ("Id")
);

-- -----------------------------------------------------------------------------
-- Materials  (extracted PDF text, used to re-generate worksheets)
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS "Materials" (
    "Id"               TEXT        NOT NULL,
    "Title"            TEXT        NOT NULL DEFAULT '',
    "Subject"          TEXT        NOT NULL DEFAULT '',
    "ClassName"        TEXT        NOT NULL DEFAULT '',
    "OriginalFileName" TEXT        NOT NULL DEFAULT '',
    "ExtractedText"    TEXT        NOT NULL DEFAULT '',
    "UploadedAt"       TIMESTAMP   NOT NULL DEFAULT NOW(),
    CONSTRAINT "PK_Materials" PRIMARY KEY ("Id")
);

-- -----------------------------------------------------------------------------
-- Worksheets
-- Questions are stored as a JSON blob in the "Questions" column.
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS "Worksheets" (
    "Id"                TEXT        NOT NULL,
    "Title"             TEXT        NOT NULL DEFAULT '',
    "Subject"           TEXT        NOT NULL DEFAULT '',
    "Topic"             TEXT        NOT NULL DEFAULT '',
    "Instructions"      TEXT        NOT NULL DEFAULT '',
    "EstimatedMinutes"  INTEGER     NOT NULL DEFAULT 0,
    "StudentProfileId"  TEXT        NOT NULL DEFAULT '',
    "SourceFileName"    TEXT        NOT NULL DEFAULT '',
    "SessionMaterialId" TEXT        NOT NULL DEFAULT '',
    "GeneratedAt"       TIMESTAMP   NOT NULL DEFAULT NOW(),
    "Questions"         TEXT        NOT NULL DEFAULT '[]',
    CONSTRAINT "PK_Worksheets" PRIMARY KEY ("Id")
);

-- -----------------------------------------------------------------------------
-- Templates
-- QuestionTypes is stored as a JSON blob.
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS "Templates" (
    "Id"                  TEXT        NOT NULL,
    "Name"                TEXT        NOT NULL DEFAULT '',
    "Description"         TEXT        NOT NULL DEFAULT '',
    "SpecialInstructions" TEXT        NOT NULL DEFAULT '',
    "Difficulty"          TEXT        NOT NULL DEFAULT 'mixed',
    "CreatedAt"           TIMESTAMP   NOT NULL DEFAULT NOW(),
    "UpdatedAt"           TIMESTAMP   NOT NULL DEFAULT NOW(),
    "QuestionTypes"       TEXT        NOT NULL DEFAULT '[]',
    CONSTRAINT "PK_Templates" PRIMARY KEY ("Id")
);
