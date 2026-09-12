-- SQL Server schema. Naming: tbl_ + plural (domain), lkp_ + plural (lookups). See data-layer.md.
-- Agents read tbl_EvaluationJobs (and tbl_EvaluationRuns) for work and status.
-- File bytes live in Azure Blob; StorageKey is the blob path (folder + file name with extension).

CREATE TABLE tbl_Rfqs (
    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    Number NVARCHAR(64) NOT NULL,
    Title NVARCHAR(500) NOT NULL,
    Description NVARCHAR(MAX) NULL,
    Status NVARCHAR(64) NOT NULL,
    CreatedAt DATETIME2 NOT NULL,
    UpdatedAt DATETIME2 NOT NULL
);
CREATE INDEX IX_tbl_Rfqs_Number ON tbl_Rfqs (Number);

CREATE TABLE tbl_RfqDocuments (
    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    RfqId INT NOT NULL,
    Kind NVARCHAR(64) NOT NULL,
    FileName NVARCHAR(512) NOT NULL,
    ContentType NVARCHAR(256) NOT NULL,
    SizeBytes BIGINT NOT NULL,
    StorageKey NVARCHAR(1024) NOT NULL,
    UploadedAt DATETIME2 NOT NULL,
    CONSTRAINT FK_tbl_RfqDocuments_Rfqs FOREIGN KEY (RfqId) REFERENCES tbl_Rfqs (Id),
    CONSTRAINT UQ_tbl_RfqDocuments_Rfq_Kind UNIQUE (RfqId, Kind)
);

CREATE TABLE tbl_Vendors (
    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    RfqId INT NOT NULL,
    Name NVARCHAR(256) NOT NULL,
    ExternalProjectId NVARCHAR(64) NULL,
    Status NVARCHAR(64) NOT NULL,
    CreatedAt DATETIME2 NOT NULL,
    UpdatedAt DATETIME2 NOT NULL,
    CONSTRAINT FK_tbl_Vendors_Rfqs FOREIGN KEY (RfqId) REFERENCES tbl_Rfqs (Id)
);
CREATE INDEX IX_tbl_Vendors_Rfq_Name ON tbl_Vendors (RfqId, Name);

CREATE TABLE tbl_VendorDocuments (
    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    VendorId INT NOT NULL,
    Kind NVARCHAR(64) NOT NULL,
    FileName NVARCHAR(512) NOT NULL,
    ContentType NVARCHAR(256) NOT NULL,
    SizeBytes BIGINT NOT NULL,
    StorageKey NVARCHAR(1024) NOT NULL,
    UploadedAt DATETIME2 NOT NULL,
    CONSTRAINT FK_tbl_VendorDocuments_Vendors FOREIGN KEY (VendorId) REFERENCES tbl_Vendors (Id)
);

CREATE TABLE tbl_EvaluationRuns (
    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    RfqId INT NOT NULL,
    Status NVARCHAR(64) NOT NULL,
    StatusMessage NVARCHAR(1000) NOT NULL,
    RequestedBy NVARCHAR(256) NULL,
    CreatedAt DATETIME2 NOT NULL,
    StartedAt DATETIME2 NULL,
    CompletedAt DATETIME2 NULL,
    CONSTRAINT FK_tbl_EvaluationRuns_Rfqs FOREIGN KEY (RfqId) REFERENCES tbl_Rfqs (Id)
);
CREATE INDEX IX_tbl_EvaluationRuns_Rfq_Status ON tbl_EvaluationRuns (RfqId, Status);

CREATE TABLE tbl_EvaluationJobs (
    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    RunId INT NOT NULL,
    RfqId INT NOT NULL,
    VendorId INT NULL,
    JobType NVARCHAR(64) NOT NULL,
    Status NVARCHAR(64) NOT NULL,
    StatusMessage NVARCHAR(1000) NULL,
    CreatedAt DATETIME2 NOT NULL,
    StartedAt DATETIME2 NULL,
    CompletedAt DATETIME2 NULL,
    CONSTRAINT FK_tbl_EvaluationJobs_Runs FOREIGN KEY (RunId) REFERENCES tbl_EvaluationRuns (Id),
    CONSTRAINT FK_tbl_EvaluationJobs_Rfqs FOREIGN KEY (RfqId) REFERENCES tbl_Rfqs (Id),
    CONSTRAINT FK_tbl_EvaluationJobs_Vendors FOREIGN KEY (VendorId) REFERENCES tbl_Vendors (Id)
);
CREATE INDEX IX_tbl_EvaluationJobs_Queue ON tbl_EvaluationJobs (Status, JobType, CreatedAt);

CREATE TABLE tbl_EvaluationJobEvents (
    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    JobId INT NOT NULL,
    Status NVARCHAR(64) NOT NULL,
    Message NVARCHAR(1000) NULL,
    At DATETIME2 NOT NULL,
    CONSTRAINT FK_tbl_EvaluationJobEvents_Jobs FOREIGN KEY (JobId) REFERENCES tbl_EvaluationJobs (Id)
);
CREATE INDEX IX_tbl_EvaluationJobEvents_Job_At ON tbl_EvaluationJobEvents (JobId, At);

CREATE TABLE tbl_ProposalSummaries (
    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    JobId INT NOT NULL,
    VendorId INT NOT NULL,
    Body NVARCHAR(MAX) NOT NULL,
    Status NVARCHAR(64) NOT NULL,
    RejectionReason NVARCHAR(2000) NULL,
    CreatedAt DATETIME2 NOT NULL,
    ReviewedAt DATETIME2 NULL,
    CONSTRAINT FK_tbl_ProposalSummaries_Jobs FOREIGN KEY (JobId) REFERENCES tbl_EvaluationJobs (Id),
    CONSTRAINT FK_tbl_ProposalSummaries_Vendors FOREIGN KEY (VendorId) REFERENCES tbl_Vendors (Id)
);

-- Criteria for this RFQ (from the scoring-strategy file). Add a row, not a column.
CREATE TABLE tbl_RfqCriteria (
    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    RfqId INT NOT NULL,
    Code NVARCHAR(32) NOT NULL,
    Name NVARCHAR(256) NOT NULL,
    Weight DECIMAL(5,2) NULL,
    GroupCode NVARCHAR(32) NULL,
    GroupWeight DECIMAL(5,2) NULL,
    SortOrder INT NOT NULL,
    CONSTRAINT FK_tbl_RfqCriteria_Rfqs FOREIGN KEY (RfqId) REFERENCES tbl_Rfqs (Id),
    CONSTRAINT UQ_tbl_RfqCriteria_Rfq_Code UNIQUE (RfqId, Code)
);

-- One score sheet per vendor per scoring job.
CREATE TABLE tbl_VendorEvaluations (
    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    JobId INT NOT NULL,
    VendorId INT NOT NULL,
    MandatoryPass BIT NOT NULL,
    Recommendation NVARCHAR(MAX) NULL,
    Status NVARCHAR(64) NOT NULL,
    CreatedAt DATETIME2 NOT NULL,
    ApprovedAt DATETIME2 NULL,
    CONSTRAINT FK_tbl_VendorEvaluations_Jobs FOREIGN KEY (JobId) REFERENCES tbl_EvaluationJobs (Id),
    CONSTRAINT FK_tbl_VendorEvaluations_Vendors FOREIGN KEY (VendorId) REFERENCES tbl_Vendors (Id),
    CONSTRAINT UQ_tbl_VendorEvaluations_Job_Vendor UNIQUE (JobId, VendorId)
);

-- Tall: one row per criterion. TWS is computed in the app, not stored.
CREATE TABLE tbl_VendorScores (
    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    VendorEvaluationId INT NOT NULL,
    RfqCriterionId INT NOT NULL,
    Score DECIMAL(5,2) NOT NULL,
    Justification NVARCHAR(MAX) NULL,
    CONSTRAINT FK_tbl_VendorScores_Evaluations FOREIGN KEY (VendorEvaluationId) REFERENCES tbl_VendorEvaluations (Id),
    CONSTRAINT FK_tbl_VendorScores_Criteria FOREIGN KEY (RfqCriterionId) REFERENCES tbl_RfqCriteria (Id),
    CONSTRAINT UQ_tbl_VendorScores_Eval_Criterion UNIQUE (VendorEvaluationId, RfqCriterionId),
    CONSTRAINT CK_tbl_VendorScores_Score CHECK (Score >= 0 AND Score <= 1)
);
