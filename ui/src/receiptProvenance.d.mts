export type Provenance = 'unread' | 'ocr' | 'calculated' | 'manual' | 'verified';
export function fieldProvenance(args: { value: unknown; source?: Record<string, any> | null; changed?: boolean; verified?: boolean; calculated?: boolean }): Provenance;
export function attentionFields(values: Record<string, unknown>, sources: Record<string, any>, changedFields?: string[], verified?: boolean, required?: string[]): string[];
