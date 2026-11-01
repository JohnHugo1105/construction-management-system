/** A project's state, as the API writes it. */
export type ProjectStatus = 'Planning' | 'Active' | 'OnHold' | 'Completed' | 'Archived';

export type ArchiveReason = 'Finished' | 'Other';

export type SubProjectStatus = 'Active' | 'Archived';

/** A part of a project (a building, a phase). */
export interface SubProject {
  readonly id: string;
  readonly name: string;
  /** A business date, "2026-11-02". */
  readonly startDate: string;
  readonly status: SubProjectStatus;
}

/** One row of the project list. */
export interface ProjectListItem {
  readonly id: string;
  readonly name: string;
  readonly client: string | null;
  readonly location: string | null;
  readonly status: ProjectStatus;
  readonly startDate: string;
  readonly targetEndDate: string | null;
  /** The agreed contract price including VAT, in minor units (centavos). */
  readonly contractPriceMinor: number | null;
  readonly subProjectCount: number;
  readonly version: number;
}

/** A whole project, as `GET /api/projects/{id}` returns it. Every change sends back the `version` it was loaded with. */
export interface Project {
  readonly id: string;
  readonly name: string;
  readonly client: string | null;
  readonly location: string | null;
  readonly contractPriceMinor: number | null;
  readonly startDate: string;
  readonly targetEndDate: string | null;
  readonly status: ProjectStatus;
  readonly archiveReason: ArchiveReason | null;
  readonly archiveNote: string | null;
  readonly archivedAtUtc: string | null;
  readonly subProjects: readonly SubProject[];
  readonly version: number;
  /**
   * The budget is frozen as the baseline: budget lines and the contract price change only through approved change orders. The API
   * always sends it; it is optional here so older fixtures read as "not locked".
   */
  readonly budgetBaselineLocked?: boolean;
}

/** What the project form sends to create a project. */
export interface ProjectDetails {
  readonly name: string;
  readonly client: string | null;
  readonly location: string | null;
  readonly contractPriceMinor: number | null;
  readonly startDate: string;
  readonly targetEndDate: string | null;
}

export interface ProjectListParams {
  readonly search: string;
  /** Empty for every project except archived ones. */
  readonly status: string;
  readonly page: number;
  readonly pageSize: number;
}

export interface ArchiveRequest {
  readonly reason: ArchiveReason;
  readonly note: string | null;
  readonly version: number;
}

export interface SubProjectDetails {
  readonly name: string;
  readonly startDate: string;
}

/** The lifecycle steps that need nothing but the project's version. */
export type ProjectTransition = 'start' | 'hold' | 'resume' | 'complete' | 'reopen' | 'restore';
