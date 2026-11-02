/**
 * Permission names, exactly as the API returns them in the session (see src/ConstructOPX.Application/Common/Permissions.cs).
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
  purchasingReceive: 'purchasing.receive',
  bondsRead: 'bonds.read',
  bondsManage: 'bonds.manage',
  payrollRulesManage: 'payrollrules.manage',
  tasksUse: 'tasks.use',
  notificationsRead: 'notifications.read',
  diaryRead: 'diary.read',
  diaryWrite: 'diary.write',
  documentsRead: 'documents.read',
  documentsManage: 'documents.manage',
  scheduleRead: 'schedule.read',
  scheduleWrite: 'schedule.write',
  timeClockReview: 'timeclock.review',
  timeClockManage: 'timeclock.manage',
  receivingRecord: 'receiving.record',
  actionsRead: 'actions.read',
} as const;

export type PermissionName = (typeof Permission)[keyof typeof Permission];

/**
 * The permissions that can put something on a person's "Needs your action" page (each one acts on a group there). Everyone holds
 * `actions.read`, but a person with none of these would only ever see an empty page, so the entry and its badge are shown only to
 * someone who holds at least one.
 */
export const NEEDS_ACTION_PERMISSIONS: readonly string[] = [
  Permission.purchasingApprove,
  Permission.changeOrdersApprove,
  Permission.billingApprove,
  Permission.billingManage,
  Permission.subcontractsApprove,
  Permission.payrollApprove,
  Permission.purchasingReceive,
  Permission.receivingRecord,
  Permission.bondsManage,
  Permission.progressWrite,
  Permission.attendanceWrite,
  Permission.statutoryManage,
  Permission.tasksUse,
  Permission.diaryWrite,
  Permission.documentsManage,
  Permission.scheduleWrite,
  Permission.timeClockReview,
];
