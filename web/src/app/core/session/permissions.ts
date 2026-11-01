/**
 * Permission names, exactly as the API returns them in the session (see src/Cms.Application/Common/Permissions.cs).
 * Use these constants with `permissionGuard(...)` and `SessionState.can(...)` instead of typing the strings.
 */
export const Permission = {
  projectsRead: 'projects.read',
  projectsManage: 'projects.manage',
  costsRead: 'costs.read',
  costsWrite: 'costs.write',
  workforceRead: 'workforce.read',
  workforceManage: 'workforce.manage',
  attendanceWrite: 'attendance.write',
  payrollRead: 'payroll.read',
  payrollRun: 'payroll.run',
  payrollApprove: 'payroll.approve',
  inventoryRead: 'inventory.read',
  inventoryManage: 'inventory.manage',
  usersManage: 'users.manage',
  settingsManage: 'settings.manage',
  auditRead: 'audit.read',
  suppliersRead: 'suppliers.read',
  suppliersManage: 'suppliers.manage',
  purchasingRead: 'purchasing.read',
  purchasingManage: 'purchasing.manage',
  purchasingApprove: 'purchasing.approve',
  progressRead: 'progress.read',
  progressWrite: 'progress.write',
  changeOrdersRead: 'changeorders.read',
  changeOrdersManage: 'changeorders.manage',
  changeOrdersApprove: 'changeorders.approve',
  billingRead: 'billing.read',
  billingManage: 'billing.manage',
  billingApprove: 'billing.approve',
  subcontractsRead: 'subcontracts.read',
  subcontractsManage: 'subcontracts.manage',
  subcontractsApprove: 'subcontracts.approve',
  statutoryManage: 'statutory.manage',
} as const;

export type PermissionName = (typeof Permission)[keyof typeof Permission];
