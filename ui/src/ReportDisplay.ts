import { formatMoney } from './decimal.mjs';

export const money = (n: any) =>
  formatMoney(n ?? '0', localStorage.getItem('decont.language') === 'en' ? 'en-GB' : 'ro-RO');
export const stateText: Record<string, [string, string]> = {
  DRAFT: ['Ciornă', 'Draft'],
  MANAGER_REVIEW: ['La manager', 'Manager review'],
  REJECTED: ['Respins', 'Rejected'],
  EMPLOYEE_CORRECTION: ['De corectat', 'Needs correction'],
  FINANCE_REVIEW: ['La Finance', 'Finance review'],
  REAPPROVAL_REQUIRED: ['Reaprobare', 'Reapproval'],
  READY_FOR_ERP: ['Gata de export', 'Ready for ERP'],
  ERP_EXPORTED: ['Exportat ERP', 'ERP exported'],
  POSTED: ['Postat', 'Posted'],
  CLOSED: ['Închis', 'Closed'],
};
