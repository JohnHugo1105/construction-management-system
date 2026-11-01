/** A role a person can hold, in the words people use. Names match the API's role names. */
export interface RoleInfo {
  readonly name: string;
  readonly label: string;
  /** One plain line about what the role is for, shown where roles are chosen. */
  readonly description: string;
}

export const ROLES: readonly RoleInfo[] = [
  {
    name: 'Administrator',
    label: 'Administrator',
    description: 'Everything, including people and company settings.',
  },
  {
    name: 'ProjectManager',
    label: 'Project Manager',
    description: 'Manages projects and records attendance.',
  },
  {
    name: 'CostEngineer',
    label: 'Cost Engineer',
    description: 'Plans budgets and records project costs.',
  },
  {
    name: 'Accountant',
    label: 'Accountant',
    description: 'Records costs, runs payroll and reads the activity log.',
  },
  {
    name: 'WarehouseKeeper',
    label: 'Warehouse Keeper',
    description: 'Manages stock and warehouse records.',
  },
  {
    name: 'HrOfficer',
    label: 'HR Officer',
    description: 'Manages workers, attendance and payroll.',
  },
  {
    name: 'Viewer',
    label: 'Viewer',
    description: 'Can look at projects, costs, workers and stock, but not change them.',
  },
];

/** "ProjectManager" becomes "Project Manager". An unknown role is spaced out rather than hidden. */
export function roleLabel(name: string): string {
  const known = ROLES.find((role) => role.name === name);
  return known ? known.label : name.replace(/([a-z])([A-Z])/g, '$1 $2');
}
