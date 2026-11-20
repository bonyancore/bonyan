export const en = {
  app: {
    name: 'Construction Connect',
  },
  nav: {
    home: 'Home',
    projects: 'Projects',
    tasks: 'Tasks',
    approvals: 'Approvals',
    create: 'Create',
    procurement: 'Procurement',
    finance: 'Finance',
    reports: 'Reports',
    settings: 'Settings',
  },
  scope: {
    company: 'Company',
    project: 'Project',
    mine: 'Mine',
  },
  status: {
    onTrack: 'On track',
    inProgress: 'In progress',
    atRisk: 'At risk',
    blocked: 'Blocked',
    done: 'Done',
    draft: 'Draft',
    awaiting: 'Awaiting decision by',
  },
  action: {
    approve: 'Approve',
    reject: 'Reject',
    saveDraft: 'Save draft',
    send: 'Send',
    cancel: 'Cancel',
    back: 'Back',
    seeAll: 'See all',
    retry: 'Retry',
    undo: 'Undo',
    dismiss: 'Dismiss',
    report: 'Report',
    request: 'Request',
  },
  sync: {
    synced: 'Synced',
    offline: 'Offline',
    offlineQueued: 'Offline, {{count}} changes queued',
  },
} as const;

export type Resources = typeof en;
