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
} as const;

export type PermissionName = (typeof Permission)[keyof typeof Permission];
