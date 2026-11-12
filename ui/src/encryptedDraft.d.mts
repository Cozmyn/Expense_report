export const DRAFT_RECOVERY_DAYS: number;
export type EncryptedSnapshot = { data: Record<string, unknown>, taxes: Record<string, unknown>, sourceVersion?: number };
export type RecoveredDraft = { id: string, updatedAt: number, ownTab: boolean, snapshot: EncryptedSnapshot };
export function encryptDraftSnapshot(snapshot: EncryptedSnapshot, key: CryptoKey): Promise<{ iv: Uint8Array, cipher: Uint8Array }>;
export function decryptDraftSnapshot(record: { iv: Uint8Array | number[], cipher: Uint8Array | number[] }, key: CryptoKey): Promise<EncryptedSnapshot>;
export function stageEncryptedDraft(scope: string, snapshot: EncryptedSnapshot): Promise<string>;
export function recoverEncryptedDrafts(scope: string): Promise<RecoveredDraft[]>;
export function clearOwnEncryptedDraftIfMatch(scope: string, snapshot: EncryptedSnapshot): Promise<boolean>;
export function discardEncryptedDraft(id: string): Promise<void>;
