export function normalizedCui(value: unknown): string;
export function adoptSupplierLookup<T extends { cui?: string | null; supplier?: string | null; supplierConfirmed?: boolean }>(
  current: T,
  lookup: { found?: boolean; valid?: boolean; name?: string | null; cui?: string | null },
  requestedCui: string
): T;
export function offersRegistryName(
  current: { cui?: string | null; supplier?: string | null },
  lookup: { found?: boolean; valid?: boolean; name?: string | null; cui?: string | null } | null
): boolean;
export function draftSnapshot<T>(
  data: T,
  taxes: Record<string, unknown>,
  sourceVersion: unknown
): { data: T; taxes: Record<string, unknown>; sourceVersion: unknown };
