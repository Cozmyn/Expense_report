CREATE TABLE IF NOT EXISTS "AspNetRoles" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_AspNetRoles" PRIMARY KEY,
    "Name" TEXT NULL,
    "NormalizedName" TEXT NULL,
    "ConcurrencyStamp" TEXT NULL
);


CREATE TABLE IF NOT EXISTS "AspNetUsers" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_AspNetUsers" PRIMARY KEY,
    "Name" TEXT NOT NULL,
    "Worker" TEXT NOT NULL,
    "CostCenter" TEXT NOT NULL,
    "ApprovalMode" TEXT NOT NULL,
    "FixedApproverId" TEXT NULL,
    "ManagerId" TEXT NULL,
    "Active" INTEGER NOT NULL,
    "ChangePassword" INTEGER NOT NULL,
    "UserName" TEXT NULL,
    "NormalizedUserName" TEXT NULL,
    "Email" TEXT NULL,
    "NormalizedEmail" TEXT NULL,
    "EmailConfirmed" INTEGER NOT NULL,
    "PasswordHash" TEXT NULL,
    "SecurityStamp" TEXT NULL,
    "ConcurrencyStamp" TEXT NULL,
    "PhoneNumber" TEXT NULL,
    "PhoneNumberConfirmed" INTEGER NOT NULL,
    "TwoFactorEnabled" INTEGER NOT NULL,
    "LockoutEnd" TEXT NULL,
    "LockoutEnabled" INTEGER NOT NULL,
    "AccessFailedCount" INTEGER NOT NULL
);


CREATE TABLE IF NOT EXISTS "Assignments" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Assignments" PRIMARY KEY,
    "UserId" TEXT NOT NULL,
    "ManagerId" TEXT NOT NULL,
    "OrganizationCode" TEXT NOT NULL,
    "Active" INTEGER NOT NULL,
    "From" TEXT NOT NULL,
    "To" TEXT NULL
);


CREATE TABLE IF NOT EXISTS "Audits" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_Audits" PRIMARY KEY AUTOINCREMENT,
    "ReportId" TEXT NULL,
    "Actor" TEXT NOT NULL,
    "Entity" TEXT NOT NULL,
    "Action" TEXT NOT NULL,
    "Before" TEXT NULL,
    "After" TEXT NULL,
    "Reason" TEXT NULL,
    "At" TEXT NOT NULL
);


CREATE TABLE IF NOT EXISTS "Delegations" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Delegations" PRIMARY KEY,
    "ManagerId" TEXT NOT NULL,
    "DelegateId" TEXT NOT NULL,
    "From" TEXT NOT NULL,
    "To" TEXT NOT NULL
);


CREATE TABLE IF NOT EXISTS "FinanceAcceptanceRecords" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_FinanceAcceptanceRecords" PRIMARY KEY,
    "Company" TEXT NOT NULL,
    "ConfigurationHash" TEXT NOT NULL,
    "Json" TEXT NOT NULL,
    "Actor" TEXT NOT NULL,
    "Reason" TEXT NOT NULL,
    "At" TEXT NOT NULL
);


CREATE TABLE IF NOT EXISTS "FinancePolicies" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_FinancePolicies" PRIMARY KEY,
    "Currency" TEXT NOT NULL,
    "Version" INTEGER NOT NULL,
    "From" TEXT NOT NULL,
    "Json" TEXT NOT NULL,
    "Actor" TEXT NOT NULL,
    "Reason" TEXT NOT NULL,
    "At" TEXT NOT NULL
);


CREATE TABLE IF NOT EXISTS "Idempotencies" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Idempotencies" PRIMARY KEY,
    "Actor" TEXT NOT NULL,
    "RequestHash" TEXT NOT NULL,
    "Response" TEXT NOT NULL,
    "At" TEXT NOT NULL
);


CREATE TABLE IF NOT EXISTS "Jobs" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Jobs" PRIMARY KEY,
    "StartedAt" TEXT NULL,
    "CompletedAt" TEXT NULL,
    "FailureCode" TEXT NULL,
    "Kind" TEXT NOT NULL,
    "Json" TEXT NOT NULL,
    "State" TEXT NOT NULL,
    "Attempts" INTEGER NOT NULL,
    "Due" TEXT NOT NULL,
    "Error" TEXT NULL
);


CREATE TABLE IF NOT EXISTS "Masters" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Masters" PRIMARY KEY,
    "Kind" TEXT NOT NULL,
    "Code" TEXT NOT NULL,
    "NameRo" TEXT NOT NULL,
    "NameEn" TEXT NOT NULL,
    "Json" TEXT NOT NULL,
    "From" TEXT NOT NULL,
    "To" TEXT NULL,
    "Active" INTEGER NOT NULL
);


CREATE TABLE IF NOT EXISTS "Notices" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Notices" PRIMARY KEY,
    "Hidden" INTEGER NOT NULL,
    "UserId" TEXT NOT NULL,
    "ReportId" TEXT NOT NULL,
    "Event" TEXT NOT NULL,
    "Read" INTEGER NOT NULL,
    "At" TEXT NOT NULL
);


CREATE TABLE IF NOT EXISTS "Preferences" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Preferences" PRIMARY KEY,
    "UserId" TEXT NOT NULL,
    "Key" TEXT NOT NULL,
    "Json" TEXT NOT NULL
);


CREATE TABLE IF NOT EXISTS "Reports" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Reports" PRIMARY KEY,
    "Number" TEXT NOT NULL,
    "EmployeeId" TEXT NOT NULL,
    "ManagerId" TEXT NOT NULL,
    "Purpose" TEXT NOT NULL,
    "EmployeeName" TEXT NOT NULL,
    "EmployeeWorker" TEXT NOT NULL,
    "DepartmentCode" TEXT NOT NULL,
    "CostCenter" TEXT NOT NULL,
    "Payment" TEXT NULL,
    "CardId" TEXT NULL,
    "State" TEXT NOT NULL,
    "Version" INTEGER NOT NULL,
    "Revision" INTEGER NOT NULL,
    "ApprovedPersonal" TEXT NOT NULL,
    "Created" TEXT NOT NULL,
    "Updated" TEXT NOT NULL,
    "RetainUntil" TEXT NULL,
    "LegalHold" INTEGER NOT NULL
);


CREATE TABLE IF NOT EXISTS "SchemaReleases" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_SchemaReleases" PRIMARY KEY AUTOINCREMENT,
    "Name" TEXT NOT NULL,
    "Applied" TEXT NOT NULL
);


CREATE TABLE IF NOT EXISTS "SupplierAccounts" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_SupplierAccounts" PRIMARY KEY,
    "Cui" TEXT NOT NULL,
    "Account" TEXT NOT NULL,
    "From" TEXT NOT NULL,
    "To" TEXT NULL,
    "Actor" TEXT NOT NULL,
    "At" TEXT NOT NULL
);


CREATE TABLE IF NOT EXISTS "TaxPolicies" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_TaxPolicies" PRIMARY KEY,
    "Json" TEXT NOT NULL,
    "Author" TEXT NOT NULL,
    "Reason" TEXT NOT NULL,
    "At" TEXT NOT NULL
);


CREATE TABLE IF NOT EXISTS "Transactions" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Transactions" PRIMARY KEY,
    "CardId" TEXT NOT NULL,
    "Reference" TEXT NOT NULL,
    "Date" TEXT NOT NULL,
    "Merchant" TEXT NOT NULL,
    "Amount" TEXT NOT NULL,
    "Currency" TEXT NOT NULL,
    "SettledRon" TEXT NOT NULL
);


CREATE TABLE IF NOT EXISTS "Vendors" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Vendors" PRIMARY KEY,
    "ReportId" TEXT NOT NULL,
    "Cui" TEXT NOT NULL,
    "Supplier" TEXT NOT NULL,
    "State" TEXT NOT NULL,
    "Account" TEXT NULL
);


CREATE TABLE IF NOT EXISTS "AspNetRoleClaims" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_AspNetRoleClaims" PRIMARY KEY AUTOINCREMENT,
    "RoleId" TEXT NOT NULL,
    "ClaimType" TEXT NULL,
    "ClaimValue" TEXT NULL,
    CONSTRAINT "FK_AspNetRoleClaims_AspNetRoles_RoleId" FOREIGN KEY ("RoleId") REFERENCES "AspNetRoles" ("Id") ON DELETE CASCADE
);


CREATE TABLE IF NOT EXISTS "AspNetUserClaims" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_AspNetUserClaims" PRIMARY KEY AUTOINCREMENT,
    "UserId" TEXT NOT NULL,
    "ClaimType" TEXT NULL,
    "ClaimValue" TEXT NULL,
    CONSTRAINT "FK_AspNetUserClaims_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
);


CREATE TABLE IF NOT EXISTS "AspNetUserLogins" (
    "LoginProvider" TEXT NOT NULL,
    "ProviderKey" TEXT NOT NULL,
    "ProviderDisplayName" TEXT NULL,
    "UserId" TEXT NOT NULL,
    CONSTRAINT "PK_AspNetUserLogins" PRIMARY KEY ("LoginProvider", "ProviderKey"),
    CONSTRAINT "FK_AspNetUserLogins_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
);


CREATE TABLE IF NOT EXISTS "AspNetUserRoles" (
    "UserId" TEXT NOT NULL,
    "RoleId" TEXT NOT NULL,
    CONSTRAINT "PK_AspNetUserRoles" PRIMARY KEY ("UserId", "RoleId"),
    CONSTRAINT "FK_AspNetUserRoles_AspNetRoles_RoleId" FOREIGN KEY ("RoleId") REFERENCES "AspNetRoles" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_AspNetUserRoles_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
);


CREATE TABLE IF NOT EXISTS "AspNetUserTokens" (
    "UserId" TEXT NOT NULL,
    "LoginProvider" TEXT NOT NULL,
    "Name" TEXT NOT NULL,
    "Value" TEXT NULL,
    CONSTRAINT "PK_AspNetUserTokens" PRIMARY KEY ("UserId", "LoginProvider", "Name"),
    CONSTRAINT "FK_AspNetUserTokens_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
);


CREATE TABLE IF NOT EXISTS "CostCenterOwners" (
    "CostCenterCode" TEXT NOT NULL CONSTRAINT "PK_CostCenterOwners" PRIMARY KEY,
    "ManagerId" TEXT NOT NULL,
    "UpdatedBy" TEXT NOT NULL,
    "UpdatedAt" TEXT NOT NULL,
    CONSTRAINT "FK_CostCenterOwners_AspNetUsers_ManagerId" FOREIGN KEY ("ManagerId") REFERENCES "AspNetUsers" ("Id") ON DELETE RESTRICT
);


CREATE TABLE IF NOT EXISTS "Sessions" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Sessions" PRIMARY KEY,
    "UserId" TEXT NOT NULL,
    "Ticket" BLOB NOT NULL,
    "Expires" TEXT NOT NULL,
    CONSTRAINT "FK_Sessions_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE
);


CREATE TABLE IF NOT EXISTS "Batches" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Batches" PRIMARY KEY,
    "SnapshotJson" TEXT NOT NULL,
    "ReportVersion" INTEGER NOT NULL,
    "FinanceHash" TEXT NOT NULL,
    "BusinessKey" TEXT NOT NULL,
    "PostingFingerprint" TEXT NOT NULL,
    "Reconciliation" TEXT NOT NULL,
    "FileName" TEXT NOT NULL,
    "ReportId" TEXT NOT NULL,
    "Revision" INTEGER NOT NULL,
    "Cycle" INTEGER NOT NULL,
    "RuleVersion" TEXT NOT NULL,
    "Rows" TEXT NOT NULL,
    "Key" TEXT NOT NULL,
    "FileHash" TEXT NOT NULL,
    "Hash" TEXT NOT NULL,
    "State" TEXT NOT NULL,
    "Actor" TEXT NOT NULL,
    "Reference" TEXT NULL,
    "At" TEXT NOT NULL,
    "PostedAt" TEXT NULL,
    CONSTRAINT "FK_Batches_Reports_ReportId" FOREIGN KEY ("ReportId") REFERENCES "Reports" ("Id") ON DELETE RESTRICT
);


CREATE TABLE IF NOT EXISTS "Comments" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Comments" PRIMARY KEY,
    "ReportId" TEXT NOT NULL,
    "LineId" TEXT NULL,
    "Revision" INTEGER NOT NULL,
    "Actor" TEXT NOT NULL,
    "Text" TEXT NOT NULL,
    "At" TEXT NOT NULL,
    CONSTRAINT "FK_Comments_Reports_ReportId" FOREIGN KEY ("ReportId") REFERENCES "Reports" ("Id") ON DELETE RESTRICT
);


CREATE TABLE IF NOT EXISTS "Decisions" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Decisions" PRIMARY KEY,
    "RevisionHash" TEXT NOT NULL,
    "ReportId" TEXT NOT NULL,
    "Revision" INTEGER NOT NULL,
    "Actor" TEXT NOT NULL,
    "Action" TEXT NOT NULL,
    "Reason" TEXT NULL,
    "At" TEXT NOT NULL,
    CONSTRAINT "FK_Decisions_Reports_ReportId" FOREIGN KEY ("ReportId") REFERENCES "Reports" ("Id") ON DELETE RESTRICT
);


CREATE TABLE IF NOT EXISTS "DocumentEvents" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_DocumentEvents" PRIMARY KEY,
    "ReportId" TEXT NOT NULL,
    "DocumentId" TEXT NOT NULL,
    "Type" TEXT NOT NULL,
    "Actor" TEXT NOT NULL,
    "Payload" TEXT NOT NULL,
    "Created" TEXT NOT NULL,
    CONSTRAINT "FK_DocumentEvents_Reports_ReportId" FOREIGN KEY ("ReportId") REFERENCES "Reports" ("Id") ON DELETE RESTRICT
);


CREATE TABLE IF NOT EXISTS "Lines" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Lines" PRIMARY KEY,
    "ReportId" TEXT NOT NULL,
    "Position" INTEGER NOT NULL,
    "Archived" INTEGER NOT NULL,
    "Json" TEXT NOT NULL,
    "EstimatedRon" TEXT NOT NULL,
    "SettledRon" TEXT NULL,
    "Reconciled" INTEGER NOT NULL,
    "VatOverrideReason" TEXT NULL,
    "OverrideReason" TEXT NULL,
    "DuplicateDisposition" TEXT NULL,
    "FxVersion" TEXT NULL,
    "FinanceRon" TEXT NULL,
    "FxDecisionId" TEXT NULL,
    "CalculationId" TEXT NULL,
    "CalculatedJson" TEXT NULL,
    "CalculationHash" TEXT NULL,
    "RoundingRon" TEXT NOT NULL,
    "FinancialRounding" INTEGER NULL,
    CONSTRAINT "FK_Lines_Reports_ReportId" FOREIGN KEY ("ReportId") REFERENCES "Reports" ("Id") ON DELETE RESTRICT
);


CREATE TABLE IF NOT EXISTS "PostingReviews" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_PostingReviews" PRIMARY KEY,
    "ReportId" TEXT NOT NULL,
    "Revision" INTEGER NOT NULL,
    "Fingerprint" TEXT NOT NULL,
    "Action" TEXT NOT NULL,
    "Actor" TEXT NOT NULL,
    "Reason" TEXT NOT NULL,
    "At" TEXT NOT NULL,
    CONSTRAINT "FK_PostingReviews_Reports_ReportId" FOREIGN KEY ("ReportId") REFERENCES "Reports" ("Id") ON DELETE RESTRICT
);


CREATE TABLE IF NOT EXISTS "ReportAttachments" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_ReportAttachments" PRIMARY KEY,
    "Capture" INTEGER NOT NULL,
    "ReceiptMode" TEXT NOT NULL,
    "UploadedBy" TEXT NOT NULL,
    "ReportId" TEXT NOT NULL,
    "CoveredLines" TEXT NOT NULL,
    "Name" TEXT NOT NULL,
    "Key" TEXT NOT NULL,
    "Mime" TEXT NOT NULL,
    "Size" INTEGER NOT NULL,
    "Hash" TEXT NOT NULL,
    "Scan" TEXT NOT NULL,
    "Archived" INTEGER NOT NULL,
    "Ocr" TEXT NULL,
    "Created" TEXT NOT NULL,
    CONSTRAINT "FK_ReportAttachments_Reports_ReportId" FOREIGN KEY ("ReportId") REFERENCES "Reports" ("Id") ON DELETE RESTRICT
);


CREATE TABLE IF NOT EXISTS "ReportResets" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_ReportResets" PRIMARY KEY,
    "ReportId" TEXT NOT NULL,
    "Json" TEXT NOT NULL,
    "Actor" TEXT NOT NULL,
    "Reason" TEXT NOT NULL,
    "At" TEXT NOT NULL,
    CONSTRAINT "FK_ReportResets_Reports_ReportId" FOREIGN KEY ("ReportId") REFERENCES "Reports" ("Id") ON DELETE RESTRICT
);


CREATE TABLE IF NOT EXISTS "Reviews" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Reviews" PRIMARY KEY,
    "ReportId" TEXT NOT NULL,
    "Revision" INTEGER NOT NULL,
    "Actor" TEXT NOT NULL,
    "Checks" TEXT NOT NULL,
    "At" TEXT NOT NULL,
    CONSTRAINT "FK_Reviews_Reports_ReportId" FOREIGN KEY ("ReportId") REFERENCES "Reports" ("Id") ON DELETE RESTRICT
);


CREATE TABLE IF NOT EXISTS "Snapshots" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Snapshots" PRIMARY KEY,
    "ReportId" TEXT NOT NULL,
    "Revision" INTEGER NOT NULL,
    "Json" TEXT NOT NULL,
    "Hash" TEXT NOT NULL,
    "Actor" TEXT NOT NULL,
    "At" TEXT NOT NULL,
    CONSTRAINT "FK_Snapshots_Reports_ReportId" FOREIGN KEY ("ReportId") REFERENCES "Reports" ("Id") ON DELETE RESTRICT
);


CREATE TABLE IF NOT EXISTS "TaxPolicyDecisions" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_TaxPolicyDecisions" PRIMARY KEY,
    "PolicyId" TEXT NOT NULL,
    "Action" TEXT NOT NULL,
    "Actor" TEXT NOT NULL,
    "Reason" TEXT NOT NULL,
    "At" TEXT NOT NULL,
    CONSTRAINT "FK_TaxPolicyDecisions_TaxPolicies_PolicyId" FOREIGN KEY ("PolicyId") REFERENCES "TaxPolicies" ("Id") ON DELETE RESTRICT
);


CREATE TABLE IF NOT EXISTS "BankTreatments" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_BankTreatments" PRIMARY KEY,
    "TransactionId" TEXT NOT NULL,
    "ReportId" TEXT NOT NULL,
    "Kind" TEXT NOT NULL,
    "PrincipalRon" TEXT NOT NULL,
    "FeeRon" TEXT NOT NULL,
    "MarkupRon" TEXT NOT NULL,
    "EvidenceId" TEXT NOT NULL,
    "Fingerprint" TEXT NOT NULL,
    "Json" TEXT NOT NULL,
    "Actor" TEXT NOT NULL,
    "Reason" TEXT NOT NULL,
    "At" TEXT NOT NULL,
    CONSTRAINT "FK_BankTreatments_Transactions_TransactionId" FOREIGN KEY ("TransactionId") REFERENCES "Transactions" ("Id") ON DELETE RESTRICT
);


CREATE TABLE IF NOT EXISTS "Calculations" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Calculations" PRIMARY KEY,
    "ReportId" TEXT NOT NULL,
    "LineId" TEXT NOT NULL,
    "InputHash" TEXT NOT NULL,
    "ContextHash" TEXT NOT NULL,
    "Policy" TEXT NOT NULL,
    "Result" TEXT NOT NULL,
    "PostingJson" TEXT NOT NULL,
    "OutputHash" TEXT NOT NULL,
    "Actor" TEXT NOT NULL,
    "At" TEXT NOT NULL,
    CONSTRAINT "FK_Calculations_Lines_LineId" FOREIGN KEY ("LineId") REFERENCES "Lines" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_Calculations_Reports_ReportId" FOREIGN KEY ("ReportId") REFERENCES "Reports" ("Id") ON DELETE RESTRICT
);


CREATE TABLE IF NOT EXISTS "Evidence" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Evidence" PRIMARY KEY,
    "LineId" TEXT NOT NULL,
    "Name" TEXT NOT NULL,
    "Key" TEXT NOT NULL,
    "Mime" TEXT NOT NULL,
    "Size" INTEGER NOT NULL,
    "Hash" TEXT NOT NULL,
    "Scan" TEXT NOT NULL,
    "Archived" INTEGER NOT NULL,
    "Ocr" TEXT NULL,
    "Created" TEXT NOT NULL,
    CONSTRAINT "FK_Evidence_Lines_LineId" FOREIGN KEY ("LineId") REFERENCES "Lines" ("Id") ON DELETE RESTRICT
);


CREATE TABLE IF NOT EXISTS "FxDecisions" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_FxDecisions" PRIMARY KEY,
    "LineId" TEXT NOT NULL,
    "ProposalId" TEXT NULL,
    "InputHash" TEXT NOT NULL,
    "FinalRon" TEXT NOT NULL,
    "Source" TEXT NOT NULL,
    "Reason" TEXT NOT NULL,
    "Actor" TEXT NOT NULL,
    "At" TEXT NOT NULL,
    CONSTRAINT "FK_FxDecisions_Lines_LineId" FOREIGN KEY ("LineId") REFERENCES "Lines" ("Id") ON DELETE RESTRICT
);


CREATE TABLE IF NOT EXISTS "FxProposals" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_FxProposals" PRIMARY KEY,
    "LineId" TEXT NOT NULL,
    "InputHash" TEXT NOT NULL,
    "Source" TEXT NOT NULL,
    "Rate" TEXT NULL,
    "RonAmount" TEXT NULL,
    "TransactionId" TEXT NULL,
    "Reason" TEXT NOT NULL,
    "Actor" TEXT NOT NULL,
    "At" TEXT NOT NULL,
    CONSTRAINT "FK_FxProposals_Lines_LineId" FOREIGN KEY ("LineId") REFERENCES "Lines" ("Id") ON DELETE RESTRICT
);


CREATE TABLE IF NOT EXISTS "Matches" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Matches" PRIMARY KEY,
    "TransactionId" TEXT NOT NULL,
    "LineId" TEXT NOT NULL,
    "Amount" TEXT NOT NULL,
    "Ron" TEXT NOT NULL,
    "Actor" TEXT NOT NULL,
    CONSTRAINT "FK_Matches_Lines_LineId" FOREIGN KEY ("LineId") REFERENCES "Lines" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_Matches_Transactions_TransactionId" FOREIGN KEY ("TransactionId") REFERENCES "Transactions" ("Id") ON DELETE RESTRICT
);


CREATE TABLE IF NOT EXISTS "DocumentReviews" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_DocumentReviews" PRIMARY KEY,
    "ReportId" TEXT NOT NULL,
    "OriginalExtraction" TEXT NOT NULL,
    "PageCount" INTEGER NOT NULL,
    "Dispositions" TEXT NOT NULL,
    "Created" TEXT NOT NULL,
    CONSTRAINT "FK_DocumentReviews_ReportAttachments_Id" FOREIGN KEY ("Id") REFERENCES "ReportAttachments" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_DocumentReviews_Reports_ReportId" FOREIGN KEY ("ReportId") REFERENCES "Reports" ("Id") ON DELETE RESTRICT
);


CREATE TABLE IF NOT EXISTS "ReportRestores" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_ReportRestores" PRIMARY KEY,
    "ResetId" TEXT NOT NULL,
    "Actor" TEXT NOT NULL,
    "Reason" TEXT NOT NULL,
    "At" TEXT NOT NULL,
    CONSTRAINT "FK_ReportRestores_ReportResets_ResetId" FOREIGN KEY ("ResetId") REFERENCES "ReportResets" ("Id") ON DELETE RESTRICT
);


CREATE TABLE IF NOT EXISTS "BankTreatmentRevocations" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_BankTreatmentRevocations" PRIMARY KEY,
    "TreatmentId" TEXT NOT NULL,
    "Actor" TEXT NOT NULL,
    "Reason" TEXT NOT NULL,
    "At" TEXT NOT NULL,
    CONSTRAINT "FK_BankTreatmentRevocations_BankTreatments_TreatmentId" FOREIGN KEY ("TreatmentId") REFERENCES "BankTreatments" ("Id") ON DELETE RESTRICT
);


CREATE TABLE IF NOT EXISTS "VerifiedVatComponents" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_VerifiedVatComponents" PRIMARY KEY,
    "CalculationId" TEXT NOT NULL,
    "LineId" TEXT NOT NULL,
    "RateBps" INTEGER NOT NULL,
    "Json" TEXT NOT NULL,
    "PolicyReference" TEXT NOT NULL,
    CONSTRAINT "FK_VerifiedVatComponents_Calculations_CalculationId" FOREIGN KEY ("CalculationId") REFERENCES "Calculations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_VerifiedVatComponents_Lines_LineId" FOREIGN KEY ("LineId") REFERENCES "Lines" ("Id") ON DELETE RESTRICT
);


CREATE TABLE IF NOT EXISTS "DocumentGroups" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_DocumentGroups" PRIMARY KEY,
    "BundleId" TEXT NOT NULL,
    "SourceKey" TEXT NOT NULL,
    "State" TEXT NOT NULL,
    "Regions" TEXT NOT NULL,
    "Fields" TEXT NOT NULL,
    "Taxes" TEXT NOT NULL,
    "Warnings" TEXT NOT NULL,
    "Parents" TEXT NOT NULL,
    "ExpenseId" TEXT NULL,
    "VerifiedBy" TEXT NULL,
    "VerifiedAt" TEXT NULL,
    "CorrectedExpense" TEXT NULL,
    CONSTRAINT "FK_DocumentGroups_DocumentReviews_BundleId" FOREIGN KEY ("BundleId") REFERENCES "DocumentReviews" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_DocumentGroups_Lines_ExpenseId" FOREIGN KEY ("ExpenseId") REFERENCES "Lines" ("Id") ON DELETE RESTRICT
);


CREATE TABLE IF NOT EXISTS "ExtractionAttempts" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_ExtractionAttempts" PRIMARY KEY,
    "BundleId" TEXT NOT NULL,
    "Fingerprint" TEXT NOT NULL,
    "Json" TEXT NOT NULL,
    "Created" TEXT NOT NULL,
    CONSTRAINT "FK_ExtractionAttempts_DocumentReviews_BundleId" FOREIGN KEY ("BundleId") REFERENCES "DocumentReviews" ("Id") ON DELETE RESTRICT
);


CREATE TABLE IF NOT EXISTS "PostingLines" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_PostingLines" PRIMARY KEY,
    "BatchId" TEXT NOT NULL,
    "StableId" TEXT NOT NULL,
    "Sequence" INTEGER NOT NULL,
    "ExpenseId" TEXT NOT NULL,
    "CalculationId" TEXT NULL,
    "FxDecisionId" TEXT NULL,
    "SourceVatComponentId" TEXT NULL,
    "MappingId" TEXT NOT NULL,
    "Strategy" TEXT NOT NULL,
    "Reason" TEXT NOT NULL,
    "AccountType" TEXT NOT NULL,
    "Account" TEXT NOT NULL,
    "DebitMinor" INTEGER NOT NULL,
    "CreditMinor" INTEGER NOT NULL,
    "ExpenseSummary" INTEGER NOT NULL,
    "SourceGrossMinor" INTEGER NOT NULL,
    "Department" TEXT NOT NULL,
    "Category" TEXT NOT NULL,
    "Vendor" TEXT NOT NULL,
    "Json" TEXT NOT NULL,
    CONSTRAINT "CK_Posting_Money" CHECK ("DebitMinor" >= 0 AND "CreditMinor" >= 0 AND NOT ("DebitMinor" > 0 AND "CreditMinor" > 0)),
    CONSTRAINT "CK_Posting_Sequence" CHECK ("Sequence" > 0),
    CONSTRAINT "FK_PostingLines_Batches_BatchId" FOREIGN KEY ("BatchId") REFERENCES "Batches" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_PostingLines_Calculations_CalculationId" FOREIGN KEY ("CalculationId") REFERENCES "Calculations" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_PostingLines_FxDecisions_FxDecisionId" FOREIGN KEY ("FxDecisionId") REFERENCES "FxDecisions" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_PostingLines_Lines_ExpenseId" FOREIGN KEY ("ExpenseId") REFERENCES "Lines" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_PostingLines_Masters_MappingId" FOREIGN KEY ("MappingId") REFERENCES "Masters" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_PostingLines_VerifiedVatComponents_SourceVatComponentId" FOREIGN KEY ("SourceVatComponentId") REFERENCES "VerifiedVatComponents" ("Id") ON DELETE RESTRICT
);


CREATE INDEX IF NOT EXISTS "IX_AspNetRoleClaims_RoleId" ON "AspNetRoleClaims" ("RoleId");


CREATE UNIQUE INDEX IF NOT EXISTS "RoleNameIndex" ON "AspNetRoles" ("NormalizedName");


CREATE INDEX IF NOT EXISTS "IX_AspNetUserClaims_UserId" ON "AspNetUserClaims" ("UserId");


CREATE INDEX IF NOT EXISTS "IX_AspNetUserLogins_UserId" ON "AspNetUserLogins" ("UserId");


CREATE INDEX IF NOT EXISTS "IX_AspNetUserRoles_RoleId" ON "AspNetUserRoles" ("RoleId");


CREATE INDEX IF NOT EXISTS "EmailIndex" ON "AspNetUsers" ("NormalizedEmail");


CREATE UNIQUE INDEX IF NOT EXISTS "UserNameIndex" ON "AspNetUsers" ("NormalizedUserName");


CREATE INDEX IF NOT EXISTS "IX_Assignments_UserId_From" ON "Assignments" ("UserId", "From");


CREATE UNIQUE INDEX IF NOT EXISTS "IX_BankTreatmentRevocations_TreatmentId" ON "BankTreatmentRevocations" ("TreatmentId");


CREATE INDEX IF NOT EXISTS "IX_BankTreatments_TransactionId" ON "BankTreatments" ("TransactionId");


CREATE UNIQUE INDEX IF NOT EXISTS "IX_Batches_BusinessKey" ON "Batches" ("BusinessKey") WHERE "BusinessKey" <> '';


CREATE UNIQUE INDEX IF NOT EXISTS "IX_Batches_ReportId_Cycle" ON "Batches" ("ReportId", "Cycle");


CREATE INDEX IF NOT EXISTS "IX_Calculations_LineId" ON "Calculations" ("LineId");


CREATE INDEX IF NOT EXISTS "IX_Calculations_ReportId" ON "Calculations" ("ReportId");


CREATE INDEX IF NOT EXISTS "IX_Comments_ReportId" ON "Comments" ("ReportId");


CREATE INDEX IF NOT EXISTS "IX_CostCenterOwners_ManagerId" ON "CostCenterOwners" ("ManagerId");


CREATE INDEX IF NOT EXISTS "IX_Decisions_ReportId" ON "Decisions" ("ReportId");


CREATE INDEX IF NOT EXISTS "IX_DocumentEvents_ReportId_DocumentId_Created" ON "DocumentEvents" ("ReportId", "DocumentId", "Created");


CREATE UNIQUE INDEX IF NOT EXISTS "IX_DocumentGroups_BundleId_SourceKey" ON "DocumentGroups" ("BundleId", "SourceKey");


CREATE UNIQUE INDEX IF NOT EXISTS "IX_DocumentGroups_ExpenseId" ON "DocumentGroups" ("ExpenseId");


CREATE INDEX IF NOT EXISTS "IX_DocumentReviews_ReportId" ON "DocumentReviews" ("ReportId");


CREATE INDEX IF NOT EXISTS "IX_Evidence_LineId" ON "Evidence" ("LineId");


CREATE UNIQUE INDEX IF NOT EXISTS "IX_ExtractionAttempts_BundleId_Fingerprint" ON "ExtractionAttempts" ("BundleId", "Fingerprint");


CREATE UNIQUE INDEX IF NOT EXISTS "IX_FinancePolicies_Currency_Version" ON "FinancePolicies" ("Currency", "Version");


CREATE INDEX IF NOT EXISTS "IX_FxDecisions_LineId" ON "FxDecisions" ("LineId");


CREATE INDEX IF NOT EXISTS "IX_FxProposals_LineId" ON "FxProposals" ("LineId");


CREATE INDEX IF NOT EXISTS "IX_Lines_ReportId" ON "Lines" ("ReportId");


CREATE UNIQUE INDEX IF NOT EXISTS "IX_Masters_Kind_Code_From" ON "Masters" ("Kind", "Code", "From");


CREATE INDEX IF NOT EXISTS "IX_Matches_LineId" ON "Matches" ("LineId");


CREATE INDEX IF NOT EXISTS "IX_Matches_TransactionId" ON "Matches" ("TransactionId");


CREATE UNIQUE INDEX IF NOT EXISTS "IX_PostingLines_BatchId_ExpenseId" ON "PostingLines" ("BatchId", "ExpenseId") WHERE "ExpenseSummary" = TRUE;


CREATE UNIQUE INDEX IF NOT EXISTS "IX_PostingLines_BatchId_Sequence" ON "PostingLines" ("BatchId", "Sequence");


CREATE UNIQUE INDEX IF NOT EXISTS "IX_PostingLines_BatchId_StableId" ON "PostingLines" ("BatchId", "StableId");


CREATE INDEX IF NOT EXISTS "IX_PostingLines_CalculationId" ON "PostingLines" ("CalculationId");


CREATE INDEX IF NOT EXISTS "IX_PostingLines_ExpenseId" ON "PostingLines" ("ExpenseId");


CREATE INDEX IF NOT EXISTS "IX_PostingLines_FxDecisionId" ON "PostingLines" ("FxDecisionId");


CREATE INDEX IF NOT EXISTS "IX_PostingLines_MappingId" ON "PostingLines" ("MappingId");


CREATE INDEX IF NOT EXISTS "IX_PostingLines_SourceVatComponentId" ON "PostingLines" ("SourceVatComponentId");


CREATE INDEX IF NOT EXISTS "IX_PostingReviews_ReportId_Revision" ON "PostingReviews" ("ReportId", "Revision");


CREATE UNIQUE INDEX IF NOT EXISTS "IX_Preferences_UserId_Key" ON "Preferences" ("UserId", "Key");


CREATE INDEX IF NOT EXISTS "IX_ReportAttachments_ReportId" ON "ReportAttachments" ("ReportId");


CREATE INDEX IF NOT EXISTS "IX_ReportResets_ReportId" ON "ReportResets" ("ReportId");


CREATE UNIQUE INDEX IF NOT EXISTS "IX_ReportRestores_ResetId" ON "ReportRestores" ("ResetId");


CREATE UNIQUE INDEX IF NOT EXISTS "IX_Reports_Number" ON "Reports" ("Number");


CREATE INDEX IF NOT EXISTS "IX_Reviews_ReportId" ON "Reviews" ("ReportId");


CREATE INDEX IF NOT EXISTS "IX_Sessions_UserId" ON "Sessions" ("UserId");


CREATE UNIQUE INDEX IF NOT EXISTS "IX_Snapshots_ReportId_Revision" ON "Snapshots" ("ReportId", "Revision");


CREATE UNIQUE INDEX IF NOT EXISTS "IX_SupplierAccounts_Cui_From" ON "SupplierAccounts" ("Cui", "From");


CREATE UNIQUE INDEX IF NOT EXISTS "IX_TaxPolicyDecisions_PolicyId_Action" ON "TaxPolicyDecisions" ("PolicyId", "Action");


CREATE UNIQUE INDEX IF NOT EXISTS "IX_Transactions_CardId_Reference" ON "Transactions" ("CardId", "Reference");


CREATE UNIQUE INDEX IF NOT EXISTS "IX_Vendors_ReportId_Cui" ON "Vendors" ("ReportId", "Cui");


CREATE UNIQUE INDEX IF NOT EXISTS "IX_VerifiedVatComponents_CalculationId_RateBps" ON "VerifiedVatComponents" ("CalculationId", "RateBps");


CREATE INDEX IF NOT EXISTS "IX_VerifiedVatComponents_LineId" ON "VerifiedVatComponents" ("LineId");


