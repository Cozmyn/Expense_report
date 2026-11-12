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


