import { defineCustomElements } from 'https://cdn.jsdelivr.net/npm/@trimble-oss/moduswebcomponents@1.17.0/loader/index.js';

await defineCustomElements(undefined, {
  resourcesUrl: 'https://cdn.jsdelivr.net/npm/@trimble-oss/moduswebcomponents@1.17.0/dist/',
});

const DIRECTION_OPTIONS = [
  { label: 'Twee richtingen', value: 'TwoWay' },
  { label: 'Lokaal naar cloud', value: 'LocalToCloud' },
  { label: 'Cloud naar lokaal', value: 'CloudToLocal' },
];

const WIZARD_STEPS = [
  { label: 'Lokale map' },
  { label: 'Project' },
  { label: 'Overzicht' },
  { label: 'Uitvoering' },
];

const SHARED_STEPS = [
  { label: 'Lokale map' },
  { label: 'Doelmap' },
  { label: 'Projecten' },
  { label: 'Overzicht' },
];

const PROVISION_STEPS = [
  { label: 'Template' },
  { label: 'Lokale map' },
  { label: 'ERP-watch' },
  { label: 'Overzicht' },
];

const DEFAULT_DIRECTION_OPTIONS = [
  { label: 'Lokaal naar cloud', value: 'LocalToCloud' },
  { label: 'Twee-richtingen', value: 'TwoWay' },
];

const state = {
  status: null,
  config: null,
  projects: [],
  projectId: '',
  projectName: '',
  rootId: '',
  folderId: '',
  folderPath: '/',
  collectionProject: false,
  crumbs: [],
  localPath: '',
  localExists: false,
  localFolders: [],
  linkMode: 'existing',
  interval: 60,
  mappings: [],
  jobId: '',
  inventory: null,
  wizardStep: 0,
  browsePath: '',
  browseParent: '',
  defaultDirection: 'LocalToCloud',
  defaultRemoteParent: '/',
  listenPort: '5000',
  remoteFolderName: '',
  sharedRuleId: '',
  sharedName: '',
  sharedPath: '',
  sharedFolderName: '99_Algemeen',
  sharedProjectIds: [],
  sharedAutoCreate: true,
  sharedStep: 0,
  provisionStep: 0,
  browseTarget: 'link',
  templateId: '',
  templateName: '',
  cloneName: '',
  cloneDescription: '',
  cloneLocal: '',
  watchRoot: '',
  autoProvision: false,
  overviewJobs: [],
  overviewStats: null,
  overviewSearch: '',
  overviewStatus: 'all',
  selectedOverviewJob: null,
  detailErp: '',
  detailDescription: '',
  detailTags: '',
  detailInterval: '60',
  detailWriteTags: false,
  restoreFile: null,
};

const els = {
  alert: document.getElementById('page-alert'),
  appVersion: document.getElementById('app-version'),
  authBadge: document.getElementById('auth-badge'),
  authUser: document.getElementById('auth-user'),
  authAvatar: document.getElementById('auth-avatar'),
  authAvatarFallback: document.getElementById('auth-avatar-fallback'),
  headerProfile: document.getElementById('header-profile'),
  headerAvatar: document.getElementById('header-avatar'),
  headerAvatarFallback: document.getElementById('header-avatar-fallback'),
  headerUserName: document.getElementById('header-user-name'),
  loginBtn: document.getElementById('login-btn'),
  firstRunCard: document.getElementById('first-run-card'),
  firstRunCopy: document.getElementById('first-run-copy'),
  firstRunLogin: document.getElementById('first-run-login-btn'),
  firstRunLink: document.getElementById('first-run-link-btn'),
  projectSelect: document.getElementById('project-select'),
  collectionProject: document.getElementById('collection-project'),
  remoteTarget: document.getElementById('remote-target'),
  showNewProjectBtn: document.getElementById('show-new-project-btn'),
  localPathLabel: document.getElementById('local-path-label'),
  browseBtn: document.getElementById('browse-btn'),
  browsePath: document.getElementById('browse-path'),
  browseUp: document.getElementById('browse-up-btn'),
  browseRoots: document.getElementById('browse-roots'),
  browseList: document.getElementById('browse-list'),
  browseCancel: document.getElementById('browse-cancel-btn'),
  browseSelect: document.getElementById('browse-select-btn'),
  localScanCopy: document.getElementById('local-scan-copy'),
  mapList: document.getElementById('map-list'),
  stepper: document.getElementById('link-stepper'),
  wizardHeading: document.getElementById('wizard-heading'),
  wizardBack: document.getElementById('wizard-back-btn'),
  wizardNext: document.getElementById('wizard-next-btn'),
  confirmBtn: document.getElementById('confirm-btn'),
  newFields: document.getElementById('new-project-fields'),
  wizardCloneName: document.getElementById('wizard-clone-name'),
  wizardTemplate: document.getElementById('wizard-template-select'),
  wizardCreateBtn: document.getElementById('wizard-create-btn'),
  summaryLocal: document.getElementById('summary-local'),
  summaryProject: document.getElementById('summary-project'),
  summaryFolder: document.getElementById('summary-folder'),
  summaryFolders: document.getElementById('summary-folders'),
  execProgress: document.getElementById('exec-progress'),
  execFeed: document.getElementById('exec-feed'),
  invUpload: document.getElementById('inv-upload-count'),
  invDownload: document.getElementById('inv-download-count'),
  invSynced: document.getElementById('inv-synced-count'),
  invLoader: document.getElementById('inv-loader'),
  invCopy: document.getElementById('inv-copy'),
  prefDirection: document.getElementById('pref-direction'),
  prefRemoteParent: document.getElementById('pref-remote-parent'),
  prefSave: document.getElementById('pref-save-btn'),
  listenPort: document.getElementById('listen-port'),
  portNotice: document.getElementById('port-notice'),
  portSave: document.getElementById('port-save-btn'),
  authHealthBadge: document.getElementById('auth-health-badge'),
  authExpiry: document.getElementById('auth-expiry'),
  authRefreshUpdated: document.getElementById('auth-refresh-updated'),
  authDiagAlert: document.getElementById('auth-diag-alert'),
  authDiagBtn: document.getElementById('auth-diag-btn'),
  authRefreshBtn: document.getElementById('auth-refresh-btn'),
  authLoginBtn: document.getElementById('auth-login-btn'),
  lanBanner: document.getElementById('lan-banner'),
  lanLink: document.getElementById('lan-link'),
  backupDownloadBtn: document.getElementById('backup-download-btn'),
  restoreDropzone: document.getElementById('restore-dropzone'),
  restoreAlert: document.getElementById('restore-alert'),
  restoreBtn: document.getElementById('restore-btn'),
  restoreCancelBtn: document.getElementById('restore-cancel-btn'),
  restoreConfirmBtn: document.getElementById('restore-confirm-btn'),
  settingsBtn: document.getElementById('settings-btn'),
  activityBtn: document.getElementById('activity-btn'),
  addProjectBtn: document.getElementById('add-project-btn'),
  jobBadge: document.getElementById('job-badge'),
  jobSummary: document.getElementById('job-summary'),
  logs: document.getElementById('log-list'),
  tabs: document.getElementById('main-tabs'),
  kpiConnectionBadge: document.getElementById('kpi-connection-badge'),
  kpiConnectionCopy: document.getElementById('kpi-connection-copy'),
  kpiProjectsCopy: document.getElementById('kpi-projects-copy'),
  kpiSharedCopy: document.getElementById('kpi-shared-copy'),
  overviewSearch: document.getElementById('overview-search'),
  overviewStatusFilter: document.getElementById('overview-status-filter'),
  overviewTable: document.getElementById('overview-table'),
  kpiMappingsCopy: document.getElementById('kpi-mappings-copy'),
  kpiMappingsTotal: document.getElementById('kpi-mappings-total'),
  kpiFilesCopy: document.getElementById('kpi-files-copy'),
  kpiDiskCopy: document.getElementById('kpi-disk-copy'),
  kpiActivityCopy: document.getElementById('kpi-activity-copy'),
  detailLocal: document.getElementById('detail-local'),
  detailRemote: document.getElementById('detail-remote'),
  detailIds: document.getElementById('detail-ids'),
  detailErp: document.getElementById('detail-erp'),
  detailDescription: document.getElementById('detail-description'),
  detailTags: document.getElementById('detail-tags'),
  detailInterval: document.getElementById('detail-interval'),
  detailWriteTags: document.getElementById('detail-write-tags'),
  detailCancel: document.getElementById('detail-cancel-btn'),
  detailSave: document.getElementById('detail-save-btn'),
  projectsTable: document.getElementById('projects-table'),
  sharedOverviewTable: document.getElementById('shared-overview-table'),
  sharedName: document.getElementById('shared-name'),
  sharedPathLabel: document.getElementById('shared-path-label'),
  sharedScanCopy: document.getElementById('shared-scan-copy'),
  sharedBrowseBtn: document.getElementById('shared-browse-btn'),
  sharedFolderName: document.getElementById('shared-folder-name'),
  sharedProjectList: document.getElementById('shared-project-list'),
  sharedAutocreate: document.getElementById('shared-autocreate'),
  sharedStepper: document.getElementById('shared-stepper'),
  sharedHeading: document.getElementById('shared-heading'),
  sharedBack: document.getElementById('shared-back-btn'),
  sharedNext: document.getElementById('shared-next-btn'),
  sharedNewBtn: document.getElementById('shared-new-btn'),
  sharedDeleteBtn: document.getElementById('shared-delete-btn'),
  sharedSaveBtn: document.getElementById('shared-save-btn'),
  sharedSummaryName: document.getElementById('shared-summary-name'),
  sharedSummaryLocal: document.getElementById('shared-summary-local'),
  sharedSummaryFolder: document.getElementById('shared-summary-folder'),
  sharedSummaryProjects: document.getElementById('shared-summary-projects'),
  sharedSummaryCreate: document.getElementById('shared-summary-create'),
  sharedTable: document.getElementById('shared-table'),
  templateSelect: document.getElementById('template-select'),
  cloneName: document.getElementById('clone-name'),
  cloneDescription: document.getElementById('clone-description'),
  clonePathLabel: document.getElementById('clone-path-label'),
  cloneBrowseBtn: document.getElementById('clone-browse-btn'),
  watchPathLabel: document.getElementById('watch-path-label'),
  watchBrowseBtn: document.getElementById('watch-browse-btn'),
  watchClearBtn: document.getElementById('watch-clear-btn'),
  autoProvision: document.getElementById('auto-provision'),
  cloneBtn: document.getElementById('clone-btn'),
  provisionSettingsBtn: document.getElementById('provision-settings-btn'),
  provisionStepper: document.getElementById('provision-stepper'),
  provisionHeading: document.getElementById('provision-heading'),
  provisionBack: document.getElementById('provision-back-btn'),
  provisionNext: document.getElementById('provision-next-btn'),
  provisionSummaryName: document.getElementById('provision-summary-name'),
  provisionSummaryTemplate: document.getElementById('provision-summary-template'),
  provisionSummaryLocal: document.getElementById('provision-summary-local'),
  provisionSummaryWatch: document.getElementById('provision-summary-watch'),
};

['kpi-connection', 'kpi-projects', 'kpi-shared', 'kpi-mappings', 'kpi-files', 'kpi-disk', 'kpi-activity', 'inv-upload', 'inv-download', 'inv-synced', 'summary-card', 'shared-summary-card', 'provision-summary-card'].forEach((id) => {
  const card = document.getElementById(id);
  if (card) {
    card.bordered = false;
  }
});

function readInputString(event) {
  return event.detail?.target?.value ?? '';
}

function readInputChecked(event) {
  return Boolean(event.detail?.target?.checked);
}

function bindButton(element, handler) {
  if (!element) {
    return;
  }
  let lock = false;
  const wrapped = (event) => {
    if (lock || element.disabled || element.hidden) {
      return;
    }
    lock = true;
    Promise.resolve()
      .then(() => handler(event))
      .catch((error) => showAlert('error', error.message))
      .finally(() => {
        lock = false;
      });
  };
  element.addEventListener('buttonClick', wrapped);
  element.addEventListener('click', wrapped);
}

function showAlert(type, message) {
  if (!message) {
    els.alert.hidden = true;
    return;
  }
  els.alert.hidden = false;
  els.alert.variant = type === 'error' ? 'error' : type;
  els.alert.alertDescription = message;
}

function setTab(index) {
  els.tabs.activeTabIndex = index;
}

function projectLabel(project) {
  return project.location ? `${project.name} (${project.location})` : project.name;
}

function projectOptions() {
  return [
    { label: 'Kies een Trimble Connect project...', value: '', disabled: true },
    ...state.projects.map((project) => ({
      label: projectLabel(project),
      value: project.id,
    })),
  ];
}

function projectById(id) {
  return state.projects.find((item) => item.id === id);
}

function currentJobFromConfig() {
  const jobs = state.config?.syncJobs || [];
  return jobs.find((job) => job.projectId === state.projectId || job.projectName === state.projectName) || null;
}

function joinRemote(root, subPath) {
  const base = (root || '/').replace(/\\/g, '/').replace(/\/+$/, '') || '';
  if (!subPath) {
    return base || '/';
  }
  return `${base === '' || base === '/' ? '' : base}/${subPath}`.replace(/\/+/g, '/');
}

function directionLabel(value) {
  return value === 'TwoWay' ? 'Twee-richtingen' : 'Lokaal naar cloud';
}

function renderStepper() {
  els.stepper.steps = WIZARD_STEPS.map((step, index) => ({
    label: step.label,
    content: String(index + 1),
    color: index < state.wizardStep ? 'primary' : index === state.wizardStep ? 'info' : 'neutral',
  }));
  els.wizardHeading.textContent = `Stap ${state.wizardStep + 1}: ${WIZARD_STEPS[state.wizardStep].label}`;
  for (let index = 0; index < 4; index += 1) {
    const panel = document.getElementById(`wizard-step-${index}`);
    if (!panel) {
      continue;
    }
    const inactive = index !== state.wizardStep;
    panel.classList.toggle('hidden', inactive);
    panel.toggleAttribute('hidden', inactive);
  }
  els.wizardBack.hidden = state.wizardStep === 0;
  els.wizardNext.hidden = state.wizardStep >= 2;
  els.wizardNext.disabled = state.wizardStep === 1 && !state.projectId;
  els.confirmBtn.hidden = state.wizardStep !== 2;
  if (state.wizardStep === 2) {
    renderSummary();
  }
}

function setWizardStep(index) {
  state.wizardStep = Math.max(0, Math.min(3, index));
  renderStepper();
}

function selectedMappings() {
  return state.mappings.filter((row) => row.included !== false);
}

function defaultDirection() {
  return state.defaultDirection === 'TwoWay' ? 'TwoWay' : 'LocalToCloud';
}

function renderLocalPath() {
  els.localPathLabel.textContent = state.localPath || 'Nog geen map gekozen.';
}

function renderMappingRows() {
  els.mapList.replaceChildren();
  if (!state.mappings.length) {
    const empty = document.createElement('modus-wc-typography');
    empty.size = 'sm';
    empty.textContent = 'Kies een lokale map om submappen te zien.';
    els.mapList.append(empty);
    return;
  }
  state.mappings.forEach((mapping) => {
    const row = document.createElement('div');
    row.className = 'map-row';
    const check = document.createElement('modus-wc-checkbox');
    check.size = 'sm';
    check.label = `${mapping.localSubPath || '(projectroot)'} (${mapping.fileCount ?? 0} bestanden)`;
    check.value = mapping.included !== false;
    check.addEventListener('inputChange', (event) => {
      mapping.included = readInputChecked(event);
      check.value = mapping.included;
    });
    const toggle = document.createElement('modus-wc-switch');
    toggle.size = 'sm';
    toggle.label = mapping.direction === 'TwoWay' ? 'Twee-richtingen' : 'Lokaal naar cloud';
    toggle.value = mapping.direction === 'TwoWay';
    toggle.addEventListener('inputChange', (event) => {
      mapping.direction = readInputChecked(event) ? 'TwoWay' : 'LocalToCloud';
      toggle.value = mapping.direction === 'TwoWay';
      toggle.label = mapping.direction === 'TwoWay' ? 'Twee-richtingen' : 'Lokaal naar cloud';
    });
    row.append(check, toggle);
    els.mapList.append(row);
  });
}

function mappingsFromLocalScan() {
  const folders = state.localFolders.length
    ? state.localFolders
    : [{ name: PathName(state.localPath), relativePath: '', fileCount: 0 }];
  state.mappings = folders.map((folder) => ({
    localSubPath: folder.relativePath || '',
    remoteFolderPath: joinRemote(state.folderPath, folder.relativePath || ''),
    direction: defaultDirection(),
    included: true,
    fileCount: folder.fileCount ?? 0,
  }));
  renderMappingRows();
}

function PathName(path) {
  return (path || '').split(/[\\/]/).filter(Boolean).at(-1) || path;
}

function collectionFolderPath() {
  const name = PathName(state.localPath);
  if (!name) {
    return state.defaultRemoteParent && state.defaultRemoteParent !== '/' ? state.defaultRemoteParent : '/';
  }
  return joinRemote(state.defaultRemoteParent || '/', name);
}

function syncRemoteTargetField() {
  if (els.remoteTarget) {
    els.remoteTarget.value = !state.folderPath || state.folderPath === '/' ? '' : state.folderPath;
  }
}

function applyCollectionFolder() {
  state.folderPath = collectionFolderPath();
  syncRemoteTargetField();
}

function renderSummary() {
  const maps = selectedMappings().map((row) => ({
    ...row,
    remoteFolderPath: joinRemote(state.folderPath, row.localSubPath || ''),
  }));
  els.summaryLocal.textContent = `Lokale bron: ${state.localPath || '–'}`;
  els.summaryProject.textContent = `Doel Trimble Project: ${state.projectName || '–'}`;
  els.summaryFolder.textContent = `Doelmap: ${state.folderPath || '/'}`;
  els.summaryFolders.replaceChildren();
  if (!maps.length) {
    const item = document.createElement('li');
    item.textContent = 'Geen mappen geselecteerd.';
    els.summaryFolders.append(item);
    return;
  }
  maps.forEach((row) => {
    const item = document.createElement('li');
    item.textContent = `${row.localSubPath || '(projectroot)'} → ${row.remoteFolderPath} (${directionLabel(row.direction)})`;
    els.summaryFolders.append(item);
  });
}

function appendFeed(action, text) {
  const item = document.createElement('li');
  item.className = 'status-row';
  const icon = document.createElement('modus-wc-icon');
  icon.size = 'xs';
  icon.decorative = true;
  icon.name = action === 'download' ? 'download' : action === 'upload' ? 'upload' : 'check_circle';
  const copy = document.createElement('modus-wc-typography');
  copy.size = 'sm';
  copy.textContent = text;
  item.append(icon, copy);
  els.execFeed.append(item);
  item.scrollIntoView({ block: 'nearest' });
}

function renderInventory(result) {
  state.inventory = result;
  els.invUpload.textContent = String(result.uploadCount ?? 0);
  els.invDownload.textContent = String(result.downloadCount ?? 0);
  els.invSynced.textContent = String(result.syncedCount ?? 0);
  els.invCopy.textContent = `${result.uploadCount} upload, ${result.downloadCount} download, ${result.syncedCount} al gelijk.`;
  els.execFeed.replaceChildren();
  (result.items || []).forEach((row) => {
    appendFeed(row.action, `${row.label}: ${row.relativePath}`);
  });
  if (els.execProgress) {
    els.execProgress.indeterminate = false;
    els.execProgress.value = 100;
    els.execProgress.label = 'Koppeling actief';
  }
  renderStepper();
}

function ruleProjectIds(rule) {
  const ids = (rule.targetProjectIds || []).filter(Boolean);
  if (ids.length) {
    return ids;
  }
  return (rule.syncTargets || []).map((target) => target.projectId).filter(Boolean);
}

function formatSyncTime(value) {
  if (!value) {
    return 'Nog niet';
  }
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return 'Nog niet';
  }
  return formatLocalTimestamp(date);
}

function ruleLastSync(rule) {
  const ids = new Set(ruleProjectIds(rule));
  const local = (rule.localFolderPath || '').replace(/\\/g, '/').toLowerCase();
  const stamps = (state.status?.jobs || [])
    .filter((job) => {
      const jobLocal = (job.localFolderPath || '').replace(/\\/g, '/').toLowerCase();
      return jobLocal === local && (!ids.size || ids.has(job.projectId));
    })
    .map((job) => new Date(job.lastSyncedAtUtc).getTime())
    .filter((value) => !Number.isNaN(value));
  if (!stamps.length) {
    return null;
  }
  return new Date(Math.max(...stamps)).toISOString();
}

function renderSharedProjectChecks() {
  const root = els.sharedProjectList;
  if (!root) {
    return;
  }
  root.replaceChildren();
  if (!state.projects.length) {
    const empty = document.createElement('modus-wc-typography');
    empty.size = 'sm';
    empty.textContent = 'Log in om projecten te kiezen.';
    root.append(empty);
    return;
  }
  const selected = new Set(state.sharedProjectIds);
  state.projects.forEach((project) => {
    const box = document.createElement('modus-wc-checkbox');
    box.size = 'sm';
    box.label = project.name || project.id;
    box.value = selected.has(project.id);
    box.addEventListener('inputChange', (event) => {
      const checked = readInputChecked(event);
      const ids = new Set(state.sharedProjectIds);
      if (checked) {
        ids.add(project.id);
      } else {
        ids.delete(project.id);
      }
      state.sharedProjectIds = [...ids];
      box.value = checked;
    });
    root.append(box);
  });
}

function renderSharedPath() {
  els.sharedPathLabel.textContent = state.sharedPath || 'Nog geen map gekozen.';
}

function renderClonePath() {
  els.clonePathLabel.textContent = state.cloneLocal || 'Nog geen map gekozen.';
}

function renderWatchPath() {
  els.watchPathLabel.textContent = state.watchRoot || 'Geen ERP-watchmap.';
}

function renderSharedSummary() {
  const names = state.sharedProjectIds
    .map((id) => projectById(id)?.name || id)
    .filter(Boolean);
  els.sharedSummaryName.textContent = `Naam: ${state.sharedName || '–'}`;
  els.sharedSummaryLocal.textContent = `Lokale bron: ${state.sharedPath || '–'}`;
  els.sharedSummaryFolder.textContent = `Doelmap: ${state.sharedFolderName || '99_Algemeen'}`;
  els.sharedSummaryProjects.textContent = names.length
    ? `Projecten: ${names.join(', ')}`
    : 'Projecten: –';
  els.sharedSummaryCreate.textContent = state.sharedAutoCreate !== false
    ? 'Automatisch aanmaken: ja'
    : 'Automatisch aanmaken: nee';
}

function renderSharedStepper() {
  els.sharedStepper.steps = SHARED_STEPS.map((step, index) => ({
    label: step.label,
    content: String(index + 1),
    color: index < state.sharedStep ? 'primary' : index === state.sharedStep ? 'info' : 'neutral',
  }));
  els.sharedHeading.textContent = `Stap ${state.sharedStep + 1}: ${SHARED_STEPS[state.sharedStep].label}`;
  for (let index = 0; index < SHARED_STEPS.length; index += 1) {
    const panel = document.getElementById(`shared-step-${index}`);
    if (!panel) {
      continue;
    }
    const inactive = index !== state.sharedStep;
    panel.classList.toggle('hidden', inactive);
    panel.toggleAttribute('hidden', inactive);
  }
  els.sharedBack.hidden = state.sharedStep === 0;
  els.sharedNext.hidden = state.sharedStep >= SHARED_STEPS.length - 1;
  els.sharedSaveBtn.hidden = state.sharedStep !== SHARED_STEPS.length - 1;
  els.sharedDeleteBtn.hidden = !state.sharedRuleId;
  if (state.sharedStep === 2) {
    renderSharedProjectChecks();
  }
  if (state.sharedStep === 3) {
    renderSharedSummary();
  }
  renderSharedPath();
}

function setSharedStep(index) {
  state.sharedStep = Math.max(0, Math.min(SHARED_STEPS.length - 1, index));
  renderSharedStepper();
}

function sharedStepReady(index) {
  if (index === 0) {
    if (!state.sharedName || !state.sharedPath) {
      showAlert('error', 'Vul een naam in en kies een lokale map.');
      return false;
    }
    return true;
  }
  if (index === 1) {
    if (!state.sharedFolderName) {
      showAlert('error', 'Vul de doelmap in Trimble Connect in.');
      return false;
    }
    return true;
  }
  if (index === 2 && !state.sharedProjectIds.length) {
    showAlert('error', 'Kies minstens één Trimble Connect-project.');
    return false;
  }
  return true;
}

function renderProvisionSummary() {
  els.provisionSummaryName.textContent = `Project: ${state.cloneName || '–'}`;
  els.provisionSummaryTemplate.textContent = `Template: ${state.templateName || '–'}`;
  els.provisionSummaryLocal.textContent = `Lokale map: ${state.cloneLocal || '–'}`;
  els.provisionSummaryWatch.textContent = `ERP-watchmap: ${state.watchRoot || 'geen'}`;
}

function renderProvisionStepper() {
  els.provisionStepper.steps = PROVISION_STEPS.map((step, index) => ({
    label: step.label,
    content: String(index + 1),
    color: index < state.provisionStep ? 'primary' : index === state.provisionStep ? 'info' : 'neutral',
  }));
  els.provisionHeading.textContent = `Stap ${state.provisionStep + 1}: ${PROVISION_STEPS[state.provisionStep].label}`;
  for (let index = 0; index < PROVISION_STEPS.length; index += 1) {
    const panel = document.getElementById(`provision-step-${index}`);
    if (!panel) {
      continue;
    }
    const inactive = index !== state.provisionStep;
    panel.classList.toggle('hidden', inactive);
    panel.toggleAttribute('hidden', inactive);
  }
  const last = state.provisionStep >= PROVISION_STEPS.length - 1;
  els.provisionBack.hidden = state.provisionStep === 0;
  els.provisionNext.hidden = last;
  els.provisionSettingsBtn.hidden = !last;
  els.cloneBtn.hidden = !last;
  if (last) {
    renderProvisionSummary();
  }
  renderClonePath();
  renderWatchPath();
}

function setProvisionStep(index) {
  state.provisionStep = Math.max(0, Math.min(PROVISION_STEPS.length - 1, index));
  renderProvisionStepper();
}

function provisionStepReady(index) {
  if (index === 0) {
    if (!state.templateId || !state.cloneName) {
      showAlert('error', 'Kies een template en vul een projectnaam in.');
      return false;
    }
    return true;
  }
  if (index === 1 && !state.cloneLocal) {
    showAlert('error', 'Kies de lokale projectmap.');
    return false;
  }
  return true;
}

function resetSharedForm() {
  state.sharedRuleId = '';
  state.sharedName = '';
  state.sharedPath = '';
  state.sharedFolderName = '99_Algemeen';
  state.sharedProjectIds = [];
  state.sharedAutoCreate = true;
  els.sharedName.value = '';
  els.sharedFolderName.value = '99_Algemeen';
  els.sharedAutocreate.value = true;
  if (els.sharedScanCopy) {
    els.sharedScanCopy.textContent = 'Nog niet gekozen.';
  }
  renderSharedProjectChecks();
  setSharedStep(0);
}

function loadSharedForm(rule) {
  state.sharedRuleId = rule.ruleId || '';
  state.sharedName = rule.name || '';
  state.sharedPath = rule.localFolderPath || '';
  state.sharedFolderName = rule.targetFolderName || '99_Algemeen';
  state.sharedProjectIds = ruleProjectIds(rule);
  state.sharedAutoCreate = rule.autoCreateRemoteFolder !== false;
  els.sharedName.value = state.sharedName;
  els.sharedFolderName.value = state.sharedFolderName;
  els.sharedAutocreate.value = state.sharedAutoCreate;
  if (els.sharedScanCopy) {
    els.sharedScanCopy.textContent = state.sharedPath || 'Nog niet gekozen.';
  }
  renderSharedProjectChecks();
  setSharedStep(0);
}

function renderSharedTable() {
  const rules = state.config?.sharedSyncRules || [];
  els.sharedTable.columns = [
    { id: 'name', header: 'Naam', accessor: 'name' },
    { id: 'localFolderPath', header: 'Lokale map', accessor: 'localFolderPath' },
    { id: 'targetFolderName', header: 'Doelmap', accessor: 'targetFolderName' },
    { id: 'projects', header: 'Projecten', accessor: 'projects' },
    { id: 'lastSync', header: 'Laatste sync', accessor: 'lastSync' },
  ];
  els.sharedTable.data = rules.map((rule, index) => ({
    id: rule.ruleId || String(index),
    name: rule.name || 'Centrale map',
    localFolderPath: rule.localFolderPath || '',
    targetFolderName: rule.targetFolderName || '99_Algemeen',
    projects: String(ruleProjectIds(rule).length),
    lastSync: formatSyncTime(ruleLastSync(rule)),
  }));
}

function renderDashboard() {
  const config = state.config || { syncJobs: [], sharedSyncRules: [] };
  const jobs = config.syncJobs || [];
  const rules = config.sharedSyncRules || [];
  const statusJobs = state.status?.jobs || [];

  els.projectsTable.columns = [
    { id: 'projectName', header: 'Project', accessor: 'projectName' },
    { id: 'localRoot', header: 'Lokale map', accessor: 'localRoot' },
    { id: 'mappings', header: 'Koppelingen', accessor: 'mappings' },
    { id: 'state', header: 'Status', accessor: 'state' },
  ];
  els.projectsTable.data = jobs.map((job, index) => {
    const live = statusJobs.find((item) => item.projectId === job.projectId) || statusJobs[index];
    return {
      id: job.jobId || String(index),
      projectName: job.projectName || projectById(job.projectId)?.name || 'Onbekend project',
      localRoot: job.localProjectRoot || job.localFolderPath,
      mappings: String((job.folderMappings || []).length || 1),
      state: live?.state || (job.enabled === false ? 'paused' : (state.status?.configured ? 'ready' : 'idle')),
    };
  });

  els.sharedOverviewTable.columns = [
    { id: 'name', header: 'Regel', accessor: 'name' },
    { id: 'localFolderPath', header: 'Lokale map', accessor: 'localFolderPath' },
    { id: 'targets', header: 'Doelen', accessor: 'targets' },
  ];
  els.sharedOverviewTable.data = rules.map((rule, index) => ({
    id: rule.ruleId || String(index),
    name: rule.name,
    localFolderPath: rule.localFolderPath,
    targets: `${rule.targetFolderName || '99_Algemeen'} · ${ruleProjectIds(rule).length} projecten`,
  }));
  renderSharedTable();
}

function applyConfig(config) {
  state.config = config;
  const prefs = config.wizardPreferences || {};
  state.defaultDirection = prefs.defaultSyncDirection === 'TwoWay' ? 'TwoWay' : 'LocalToCloud';
  state.defaultRemoteParent = prefs.defaultRemoteParentPath || '/';
  if (els.prefDirection) {
    els.prefDirection.value = state.defaultDirection;
  }
  if (els.prefRemoteParent) {
    els.prefRemoteParent.value = state.defaultRemoteParent === '/' ? '' : state.defaultRemoteParent;
  }
  const provisioning = config.projectProvisioning || {};
  state.templateId = provisioning.defaultTemplateProjectId || state.templateId;
  state.templateName = provisioning.defaultTemplateProjectName || state.templateName;
  state.watchRoot = provisioning.watchRoot || '';
  state.autoProvision = Boolean(provisioning.autoProvisionOnFolderTrigger);
  renderWatchPath();
  els.autoProvision.value = state.autoProvision;
  if (state.templateId) {
    els.templateSelect.value = state.templateId;
  }
  renderMappingRows();
  renderSharedTable();
  renderDashboard();
}

async function api(path, options) {
  const response = await fetch(path, {
    headers: { Accept: 'application/json', 'Content-Type': 'application/json' },
    ...options,
  });
  if (!response.ok) {
    const body = await response.json().catch(() => ({}));
    throw new Error(body.error || `HTTP ${response.status}`);
  }
  return response.json();
}

async function loadConfig() {
  try {
    applyConfig(await api('/api/setup/config'));
  } catch (error) {
    showAlert('error', error.message);
  }
}

async function saveConfig(next) {
  await api('/api/setup/config', {
    method: 'PUT',
    body: JSON.stringify(next),
  });
  applyConfig(next);
}

function displayName(status) {
  const user = status.user || {};
  const full = `${user.firstName || status.userFirstName || ''} ${user.lastName || status.userLastName || ''}`.trim();
  return full || status.userName || user.email || status.userEmail || 'Trimble ID';
}

function applyAvatar(img, fallback, enabled, name) {
  img.onerror = () => {
    img.removeAttribute('src');
    img.hidden = true;
    fallback.hidden = false;
  };
  if (!enabled) {
    img.removeAttribute('src');
    img.alt = '';
    img.hidden = true;
    fallback.hidden = false;
    return;
  }
  if (img.getAttribute('src') !== '/api/user/avatar') {
    img.src = '/api/user/avatar';
  }
  img.alt = name;
  img.hidden = false;
  fallback.hidden = true;
}

function renderAuth(status) {
  if (els.appVersion && status.version) {
    els.appVersion.textContent = status.version;
  }
  if (status.authenticated) {
    const who = displayName(status);
    const showAvatar = status.user?.hasImage !== false;
    els.authBadge.color = 'success';
    els.authBadge.textContent = 'Ingelogd';
    els.authUser.textContent = who;
    applyAvatar(els.authAvatar, els.authAvatarFallback, showAvatar, who);
    els.headerProfile.classList.remove('hidden');
    els.headerUserName.textContent = who;
    applyAvatar(els.headerAvatar, els.headerAvatarFallback, showAvatar, who);
    els.kpiConnectionBadge.color = 'success';
    els.kpiConnectionBadge.textContent = 'Verbonden';
    els.kpiConnectionCopy.textContent = who;
    els.loginBtn.hidden = true;
  } else {
    els.authBadge.color = undefined;
    els.authBadge.textContent = 'Niet ingelogd';
    els.authUser.textContent = '';
    applyAvatar(els.authAvatar, els.authAvatarFallback, false, '');
    els.authAvatarFallback.hidden = true;
    els.headerProfile.classList.add('hidden');
    els.headerUserName.textContent = '';
    els.kpiConnectionBadge.color = undefined;
    els.kpiConnectionBadge.textContent = 'Offline';
    els.kpiConnectionCopy.textContent = 'Log in om projecten te beheren.';
    els.loginBtn.hidden = false;
  }
  renderFirstRun(status);
}

function renderFirstRun(status) {
  if (!els.firstRunCard) {
    return;
  }
  const configured = Boolean(status.configured);
  els.firstRunCard.hidden = configured;
  if (configured) {
    return;
  }
  if (els.firstRunCopy) {
    els.firstRunCopy.textContent = status.authenticated
      ? 'Je bent ingelogd. Koppel nu een lokale map aan een Trimble Connect-project.'
      : 'Deze connector is leeg. Log in met je Trimble ID en koppel daarna je eerste map.';
  }
  if (els.firstRunLogin) {
    els.firstRunLogin.hidden = Boolean(status.authenticated);
  }
  if (els.firstRunLink) {
    els.firstRunLink.hidden = !status.authenticated;
  }
}

function renderLogs(status) {
  const jobs = status.jobs || [];
  const job = jobs[0];
  els.kpiProjectsCopy.textContent = String(status.projectCount ?? state.config?.syncJobs?.length ?? 0);
  els.kpiSharedCopy.textContent = String(status.sharedRuleCount ?? state.config?.sharedSyncRules?.length ?? 0);
  if (job) {
    els.jobBadge.variant = 'filled';
    els.jobBadge.color = job.state === 'error' ? 'danger'
      : job.state === 'paused' ? 'warning'
        : job.state === 'syncing' ? 'primary' : 'success';
    els.jobBadge.textContent = job.state;
    const name = job.projectName || 'Project';
    els.jobSummary.textContent = jobs.length > 1
      ? `${jobs.length} sync-taken, o.a. ${name} → ${job.remoteFolderPath || job.localFolderPath}`
      : `${name} → ${job.remoteFolderPath || '/'} (${job.localFolderPath})`;
  }
  els.logs.replaceChildren();
  const lines = status.logs?.length ? status.logs : ['Nog geen logs.'];
  for (const line of lines.slice(-40)) {
    const item = document.createElement('li');
    item.textContent = line;
    els.logs.append(item);
  }
  renderDashboard();
}

function renderFolders(folders) {
  if (!els.folderList) {
    return;
  }
  els.folderList.replaceChildren();
  if (!folders.length) {
    const empty = document.createElement('modus-wc-typography');
    empty.size = 'sm';
    empty.textContent = 'Geen submappen. De huidige remote root wordt gebruikt.';
    els.folderList.append(empty);
  }

  for (const folder of folders) {
    const button = document.createElement('modus-wc-button');
    button.size = 'sm';
    button.color = 'tertiary';
    button.variant = 'outlined';
    button.fullWidth = true;
    button.innerHTML = `<modus-wc-icon name="folder_closed" size="xs" decorative></modus-wc-icon>${folder.name}`;
    button.addEventListener('buttonClick', () => openFolder(folder));
    button.addEventListener('click', () => openFolder(folder));
    const row = document.createElement('div');
    row.className = 'folder-row';
    row.append(button);
    els.folderList.append(row);
  }

  if (els.folderCrumb) {
    els.folderCrumb.textContent = state.folderPath || '/';
  }
  if (els.folderSelected) {
    els.folderSelected.textContent = `Doelmap: ${state.folderPath || '/'}`;
  }
}

async function loadFolders(folderId, parentPath) {
  if (!state.projectId && !state.projectName) {
    return;
  }
  const query = new URLSearchParams();
  if (state.projectId) {
    query.set('projectId', state.projectId);
  }
  if (state.projectName) {
    query.set('projectName', state.projectName);
  }
  if (folderId) {
    query.set('folderId', folderId);
  }
  query.set('parentPath', parentPath || '/');
  const folders = await api(`/api/setup/folders?${query}`);
  renderFolders(folders);
}

async function openFolder(folder) {
  state.folderId = folder.id;
  state.folderPath = folder.path || folder.name;
  state.crumbs.push({ id: folder.id, name: folder.name, path: state.folderPath });
  await loadFolders(folder.id, state.folderPath);
}

async function scanLocal() {
  if (!state.localPath) {
    return { exists: false, error: 'Kies een lokale map.' };
  }
  const result = await api(`/api/setup/local-tree?path=${encodeURIComponent(state.localPath)}`);
  state.localExists = Boolean(result.exists);
  state.localFolders = result.folders || [];
  state.localPath = result.path || state.localPath;
  renderLocalPath();
  els.localScanCopy.textContent = result.exists
    ? `${result.folders?.length || 0} mappen gevonden in ${result.path}`
    : (result.error || 'Map niet gevonden.');
  if (result.exists) {
    mappingsFromLocalScan();
  } else {
    state.mappings = [];
    renderMappingRows();
  }
  return result;
}

function folderBrowserDialog() {
  return document.getElementById('folder-browser-dialog');
}

function renderBrowse(result) {
  state.browsePath = result.path || state.browsePath;
  state.browseParent = result.parent || '';
  els.browsePath.textContent = state.browsePath || '–';
  els.browseRoots.replaceChildren();
  (result.roots || []).forEach((root) => {
    const button = document.createElement('modus-wc-button');
    button.size = 'sm';
    button.color = 'tertiary';
    button.variant = 'outlined';
    button.textContent = root.name;
    bindButton(button, () => loadBrowse(root.path));
    els.browseRoots.append(button);
  });
  els.browseList.replaceChildren();
  if (result.error) {
    const copy = document.createElement('modus-wc-typography');
    copy.size = 'sm';
    copy.textContent = result.error;
    els.browseList.append(copy);
    return;
  }
  (result.folders || []).forEach((folder) => {
    const button = document.createElement('modus-wc-button');
    button.size = 'sm';
    button.color = 'tertiary';
    button.variant = 'outlined';
    button.fullWidth = true;
    button.innerHTML = `<modus-wc-icon name="folder_closed" size="xs" decorative></modus-wc-icon>${folder.name}`;
    bindButton(button, () => loadBrowse(folder.path));
    const row = document.createElement('div');
    row.className = 'folder-row';
    row.append(button);
    els.browseList.append(row);
  });
}

async function loadBrowse(path) {
  const result = await api(`/api/setup/browse?path=${encodeURIComponent(path || '')}`);
  renderBrowse(result);
}

async function openFolderBrowser(target = 'link') {
  state.browseTarget = target;
  const start = target === 'shared' ? state.sharedPath
    : target === 'clone' ? state.cloneLocal
    : target === 'watch' ? state.watchRoot
    : state.localPath;
  await loadBrowse(start || '');
  folderBrowserDialog()?.showModal();
}

function closeFolderBrowser() {
  folderBrowserDialog()?.close();
}

async function selectBrowsedFolder() {
  if (!state.browsePath) {
    showAlert('error', 'Kies een map.');
    return;
  }
  const chosen = state.browsePath;
  closeFolderBrowser();
  if (state.browseTarget === 'shared') {
    const result = await api(`/api/setup/local-tree?path=${encodeURIComponent(chosen)}`);
    state.sharedPath = result.path || chosen;
    renderSharedPath();
    els.sharedScanCopy.textContent = result.exists
      ? `${result.folders?.length || 0} submappen in ${result.path}`
      : (result.error || 'Map niet gevonden.');
    if (!result.exists) {
      showAlert('error', result.error || 'Map niet gevonden.');
      return;
    }
    showAlert(null);
    return;
  }
  if (state.browseTarget === 'clone') {
    state.cloneLocal = chosen;
    renderClonePath();
    showAlert(null);
    return;
  }
  if (state.browseTarget === 'watch') {
    state.watchRoot = chosen;
    renderWatchPath();
    showAlert(null);
    return;
  }
  state.localPath = chosen;
  renderLocalPath();
  const result = await scanLocal();
  if (!result.exists) {
    showAlert('error', result.error || 'Map niet gevonden.');
    return;
  }
  showAlert(null);
}

async function applyDefaultRemoteParent() {
  const parent = state.defaultRemoteParent && state.defaultRemoteParent !== '/'
    ? state.defaultRemoteParent
    : '';
  if (!parent) {
    return;
  }
  state.folderPath = parent;
  if (els.remoteTarget) {
    els.remoteTarget.value = parent;
  }
}

function setLinkMode(mode) {
  state.linkMode = mode;
  const hideNew = mode !== 'new';
  els.newFields?.classList.toggle('hidden', hideNew);
  els.newFields?.toggleAttribute('hidden', hideNew);
}

async function loadProjects() {
  state.projects = await api('/api/projects');
  const options = projectOptions();
  els.projectSelect.options = options;
  if (els.templateSelect) {
    els.templateSelect.options = options;
  }
  if (els.wizardTemplate) {
    els.wizardTemplate.options = options;
  }
  renderSharedProjectChecks();
  els.projectSelect.value = state.projectId || '';
  renderStepper();
  renderDashboard();
}

async function refreshStatus() {
  state.status = await api('/api/setup/status');
  renderAuth(state.status);
  renderLogs(state.status);
  await loadOverview().catch(() => undefined);
  return state.status;
}

const OVERVIEW_STATUS_OPTIONS = [
  { label: 'Alle statussen', value: 'all' },
  { label: 'In sync', value: 'OK' },
  { label: 'Fout / waarschuwing', value: 'ERROR' },
  { label: 'Gepauzeerd', value: 'PAUSED' },
];

function statusLabel(status) {
  if (status === 'SYNCING') {
    return 'Bezig';
  }
  if (status === 'ERROR') {
    return 'Fout';
  }
  if (status === 'PAUSED') {
    return 'Gepauzeerd';
  }
  return 'In sync';
}

function statusColor(status) {
  if (status === 'SYNCING') {
    return 'warning';
  }
  if (status === 'ERROR') {
    return 'danger';
  }
  if (status === 'PAUSED') {
    return 'tertiary';
  }
  return 'success';
}

function formatWhen(value) {
  if (!value) {
    return 'Nog niet gesynchroniseerd';
  }
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return value;
  }
  return formatLocalTimestamp(date);
}

function formatLocalTimestamp(date) {
  const pad = (part) => String(part).padStart(2, '0');
  return `${pad(date.getDate())}-${pad(date.getMonth() + 1)}-${date.getFullYear()}, ${pad(date.getHours())}:${pad(date.getMinutes())}:${pad(date.getSeconds())}`;
}

function filteredOverviewJobs() {
  const query = (state.overviewSearch || '').trim().toLowerCase();
  return (state.overviewJobs || []).filter((job) => {
    if (state.overviewStatus === 'ERROR' && job.syncStatus !== 'ERROR') {
      return false;
    }
    if (state.overviewStatus === 'OK' && job.syncStatus !== 'OK' && job.syncStatus !== 'SYNCING') {
      return false;
    }
    if (state.overviewStatus === 'PAUSED' && job.syncStatus !== 'PAUSED') {
      return false;
    }
    if (!query) {
      return true;
    }
    const haystack = [
      job.erpProjectId,
      job.projectName,
      job.remoteProjectName,
      job.localFolderPath,
      job.remoteFolderName,
      job.remoteFolderPath,
      job.description,
      ...(job.tags || []),
    ].join(' ').toLowerCase();
    return haystack.includes(query);
  });
}

function createText(size, text) {
  const node = document.createElement('modus-wc-typography');
  node.size = size;
  node.textContent = text;
  return node;
}

function iconButton(name, label, onClick) {
  const button = document.createElement('modus-wc-button');
  button.size = 'sm';
  button.color = 'tertiary';
  button.variant = 'borderless';
  button.shape = 'square';
  button.setAttribute('aria-label', label);
  const icon = document.createElement('modus-wc-icon');
  icon.name = name;
  icon.size = 'xs';
  icon.decorative = true;
  button.append(icon);
  bindButton(button, onClick);
  return button;
}

function renderStatusCell(_value, row) {
  const badge = document.createElement('modus-wc-badge');
  badge.size = 'sm';
  badge.variant = 'filled';
  badge.color = statusColor(row.syncStatus);
  badge.textContent = statusLabel(row.syncStatus);
  return badge;
}

function renderLocalCell(_value, row) {
  const wrap = document.createElement('div');
  wrap.className = 'path-cell';
  wrap.append(createText('sm', row.localFolderPath || '–'));
  wrap.append(iconButton('copy', 'Kopieer lokaal pad', async () => {
    try {
      await navigator.clipboard.writeText(row.localFolderPath || '');
      showAlert('success', 'Lokaal pad gekopieerd.');
    } catch (error) {
      showAlert('error', error.message);
    }
  }));
  return wrap;
}

function renderRemoteCell(_value, row) {
  const wrap = document.createElement('div');
  wrap.className = 'cell-stack';
  wrap.append(createText('sm', row.remoteProjectName || row.projectName || '–'));
  wrap.append(createText('sm', row.remoteFolderPath || '/'));
  return wrap;
}

function renderTagsCell(_value, row) {
  const wrap = document.createElement('div');
  wrap.className = 'tag-row';
  const tags = [...(row.erpProjectId ? [row.erpProjectId] : []), ...(row.tags || [])];
  if (!tags.length) {
    wrap.append(createText('sm', row.description || 'Geen tags'));
    return wrap;
  }
  tags.forEach((tag) => {
    const chip = document.createElement('modus-wc-chip');
    chip.size = 'sm';
    chip.variant = 'filled';
    chip.label = tag;
    wrap.append(chip);
  });
  return wrap;
}

function renderSyncCell(_value, row) {
  const wrap = document.createElement('div');
  wrap.className = 'cell-stack';
  wrap.append(createText('sm', formatWhen(row.lastSyncTime)));
  wrap.append(createText('sm', `${row.totalFilesSynced ?? 0} bestanden · ${row.totalSizeMb ?? 0} MB`));
  return wrap;
}

function renderActionCell(_value, row) {
  const wrap = document.createElement('div');
  wrap.className = 'cell-actions';
  wrap.append(iconButton('settings', 'Bewerk metadata', () => openMappingDetail(row)));
  wrap.append(iconButton('notifications', 'Bekijk activiteit', () => {
    window.location.href = '/activity';
  }));
  wrap.append(iconButton('refresh', 'Nu synchroniseren', () => overviewSync(row.jobId)));
  wrap.append(iconButton(row.enabled ? 'pause' : 'play', row.enabled ? 'Pauzeer map' : 'Hervat map', () => overviewToggle(row.jobId)));
  return wrap;
}

function renderOverviewStats(stats) {
  state.overviewStats = stats;
  if (els.kpiMappingsCopy) {
    els.kpiMappingsCopy.textContent = String(stats.totalActiveMappings ?? 0);
  }
  if (els.kpiMappingsTotal) {
    els.kpiMappingsTotal.textContent = `${stats.totalMappings ?? 0} mappen in totaal`;
  }
  if (els.kpiFilesCopy) {
    els.kpiFilesCopy.textContent = String(stats.totalSyncedFiles ?? 0);
  }
  if (els.kpiDiskCopy) {
    els.kpiDiskCopy.textContent = `${stats.diskUsageMb ?? 0} MB`;
  }
  if (els.kpiActivityCopy) {
    const summary = stats.lastActivity
      ? `${formatWhen(stats.lastActivity)}${stats.lastActivitySummary ? ` · ${stats.lastActivitySummary}` : ''}`
      : 'Nog geen activiteit';
    els.kpiActivityCopy.textContent = summary.length > 96 ? `${summary.slice(0, 95)}…` : summary;
  }
}

function renderOverviewTable() {
  if (!els.overviewTable) {
    return;
  }
  els.overviewTable.columns = [
    { id: 'syncStatus', header: 'Status', accessor: 'syncStatus', cellRenderer: renderStatusCell },
    { id: 'localFolderPath', header: 'Lokale map', accessor: 'localFolderPath', cellRenderer: renderLocalCell },
    { id: 'remoteFolderPath', header: 'Trimble-doel', accessor: 'remoteFolderPath', cellRenderer: renderRemoteCell },
    { id: 'tags', header: 'Metadata & tags', accessor: 'tagsLabel', cellRenderer: renderTagsCell },
    { id: 'lastSyncTime', header: 'Laatste sync', accessor: 'lastSyncLabel', cellRenderer: renderSyncCell },
    { id: 'actions', header: 'Acties', accessor: 'actions', cellRenderer: renderActionCell },
  ];
  els.overviewTable.data = filteredOverviewJobs().map((job) => ({
    ...job,
    id: job.jobId,
    tagsLabel: (job.tags || []).join(', '),
    lastSyncLabel: formatWhen(job.lastSyncTime),
    actions: '',
  }));
}

async function loadOverview() {
  const [jobs, stats] = await Promise.all([
    api('/api/overview/jobs'),
    api('/api/overview/stats'),
  ]);
  state.overviewJobs = Array.isArray(jobs) ? jobs : [];
  renderOverviewStats(stats || {});
  renderOverviewTable();
}

function mappingDetailDialog() {
  return document.getElementById('mapping-detail-dialog');
}

function openMappingDetail(job) {
  state.selectedOverviewJob = job;
  state.detailErp = job.erpProjectId || '';
  state.detailDescription = job.description || '';
  state.detailTags = (job.tags || []).join(', ');
  state.detailInterval = String(job.syncIntervalSeconds || 60);
  state.detailWriteTags = false;
  if (els.detailLocal) {
    els.detailLocal.textContent = `Lokale map: ${job.localFolderPath || '–'}`;
  }
  if (els.detailRemote) {
    els.detailRemote.textContent = `Remote: ${job.remoteProjectName || job.projectName} · ${job.remoteFolderPath || '/'}`;
  }
  if (els.detailIds) {
    els.detailIds.textContent = `Project ${job.projectId || '–'} · map ${job.remoteFolderId || '–'} · job ${job.jobId}`;
  }
  if (els.detailErp) {
    els.detailErp.value = state.detailErp;
  }
  if (els.detailDescription) {
    els.detailDescription.value = state.detailDescription;
  }
  if (els.detailTags) {
    els.detailTags.value = state.detailTags;
  }
  if (els.detailInterval) {
    els.detailInterval.value = state.detailInterval;
  }
  if (els.detailWriteTags) {
    els.detailWriteTags.value = false;
  }
  mappingDetailDialog()?.showModal();
}

function closeMappingDetail() {
  mappingDetailDialog()?.close();
}

async function overviewToggle(jobId) {
  await api('/api/overview/jobs/toggle', {
    method: 'POST',
    body: JSON.stringify({ jobId }),
  });
  await loadOverview();
}

async function overviewSync(jobId) {
  await api('/api/overview/jobs/sync', {
    method: 'POST',
    body: JSON.stringify({ jobId }),
  });
  showAlert('success', 'Handmatige sync is gestart.');
  await loadOverview();
}

async function validateStep(index) {
  if (index === 0) {
    if (!state.localExists) {
      const result = await scanLocal();
      if (!result.exists) {
        showAlert('error', result.error || 'Kies een bestaande lokale map.');
        return false;
      }
    }
    if (!selectedMappings().length) {
      showAlert('error', 'Selecteer minstens één map om te synchroniseren.');
      return false;
    }
    showAlert(null);
    return true;
  }
  if (index === 1) {
    if (!state.projectId) {
      showAlert('error', 'Kies een Trimble Connect-project.');
      return false;
    }
    showAlert(null);
    return true;
  }
  return true;
}

async function startLogin() {
  const { url } = await api('/api/setup/login-url');
  window.location.href = url;
}

function startProjectWizard() {
  state.projectId = '';
  state.projectName = '';
  state.jobId = '';
  if (els.projectSelect) {
    els.projectSelect.value = '';
  }
  setTab(2);
  setWizardStep(0);
}

bindButton(els.loginBtn, startLogin);
bindButton(els.firstRunLogin, startLogin);
bindButton(els.firstRunLink, startProjectWizard);

els.projectSelect.addEventListener('inputChange', async (event) => {
  const value = readInputString(event);
  const project = projectById(value);
  if (!project) {
    state.projectId = '';
    state.projectName = '';
    els.projectSelect.value = '';
    renderStepper();
    return;
  }
  state.projectId = project.id;
  state.projectName = project.name;
  state.rootId = project.rootId || '';
  state.folderId = project.rootId || '';
  els.projectSelect.value = value;
  if (state.collectionProject) {
    applyCollectionFolder();
  }
  renderStepper();
});

if (els.collectionProject) {
  els.collectionProject.value = state.collectionProject;
  els.collectionProject.addEventListener('inputChange', (event) => {
    state.collectionProject = readInputChecked(event);
    els.collectionProject.value = state.collectionProject;
    if (state.collectionProject) {
      applyCollectionFolder();
    } else {
      state.folderPath = state.defaultRemoteParent && state.defaultRemoteParent !== '/'
        ? state.defaultRemoteParent
        : '/';
      syncRemoteTargetField();
    }
  });
}

if (els.remoteTarget) {
  els.remoteTarget.addEventListener('inputChange', (event) => {
    const value = readInputString(event).trim();
    state.folderPath = value ? (value.startsWith('/') ? value : `/${value}`) : '/';
    els.remoteTarget.value = state.folderPath === '/' ? '' : state.folderPath;
  });
}

bindButton(els.showNewProjectBtn, () => {
  setLinkMode(state.linkMode === 'new' ? 'existing' : 'new');
});

bindButton(els.browseBtn, () => openFolderBrowser('link').catch((error) => showAlert('error', error.message)));
bindButton(els.cloneBrowseBtn, () => openFolderBrowser('clone').catch((error) => showAlert('error', error.message)));
bindButton(els.watchBrowseBtn, () => openFolderBrowser('watch').catch((error) => showAlert('error', error.message)));
bindButton(els.watchClearBtn, () => {
  state.watchRoot = '';
  renderWatchPath();
});
bindButton(els.provisionBack, () => setProvisionStep(state.provisionStep - 1));
bindButton(els.provisionNext, () => {
  if (!provisionStepReady(state.provisionStep)) {
    return;
  }
  showAlert(null);
  setProvisionStep(state.provisionStep + 1);
});
bindButton(els.browseUp, () => {
  loadBrowse(state.browseParent || '').catch((error) => showAlert('error', error.message));
});
bindButton(els.browseCancel, closeFolderBrowser);
bindButton(els.browseSelect, () => selectBrowsedFolder().catch((error) => showAlert('error', error.message)));

bindButton(els.wizardBack, () => setWizardStep(state.wizardStep - 1));
bindButton(els.wizardNext, async () => {
  if (state.wizardStep >= 2) {
    return;
  }
  if (!(await validateStep(state.wizardStep))) {
    return;
  }
  if (state.wizardStep === 0) {
    await loadProjects();
    if (state.collectionProject) {
      applyCollectionFolder();
    } else if (state.defaultRemoteParent && state.defaultRemoteParent !== '/' && (!state.folderPath || state.folderPath === '/')) {
      state.folderPath = state.defaultRemoteParent;
    }
    syncRemoteTargetField();
    els.projectSelect.value = state.projectId || '';
  }
  setWizardStep(state.wizardStep + 1);
});

bindButton(els.wizardCreateBtn, async () => {
  try {
    showAlert(null);
    const name = state.cloneName || PathName(state.localPath);
    const project = await api('/api/setup/provision', {
      method: 'POST',
      body: JSON.stringify({
        templateProjectId: state.templateId,
        templateProjectName: state.templateName,
        name,
        localFolderPath: state.localPath,
      }),
    });
    state.projectId = project.id;
    state.projectName = project.name || name;
    showAlert('success', `Project ${state.projectName} is aangemaakt.`);
    await loadProjects();
    setLinkMode('existing');
    els.projectSelect.value = state.projectId;
    renderStepper();
  } catch (error) {
    showAlert('error', error.message);
  }
});
els.wizardCloneName?.addEventListener('inputChange', (event) => {
  state.cloneName = readInputString(event);
  els.wizardCloneName.value = state.cloneName;
});
els.wizardTemplate?.addEventListener('inputChange', (event) => {
  state.templateId = readInputString(event);
  state.templateName = projectById(state.templateId)?.name || '';
  els.wizardTemplate.value = state.templateId;
});

function payloadMappings() {
  const maps = selectedMappings();
  return maps.length
    ? maps.map((row) => ({
        localSubPath: row.localSubPath || '',
        remoteFolderPath: joinRemote(state.folderPath, row.localSubPath || ''),
        direction: row.direction || defaultDirection(),
      }))
    : [{ localSubPath: '', remoteFolderPath: state.folderPath || '/', direction: defaultDirection() }];
}

bindButton(els.confirmBtn, async () => {
  try {
    showAlert(null);
    setWizardStep(3);
    els.invLoader.hidden = false;
    els.execProgress.indeterminate = true;
    els.execProgress.value = 0;
    els.execProgress.label = 'Mappen voorbereiden';
    els.invCopy.textContent = 'Koppeling opslaan en inventariseren…';
    els.execFeed.replaceChildren();
    appendFeed('synced', 'Remote mappen controleren…');
    const mappings = payloadMappings();
    const saved = await api('/api/setup/save', {
      method: 'POST',
      body: JSON.stringify({
        projectName: state.projectName,
        projectId: state.projectId,
        remoteFolderPath: mappings[0].remoteFolderPath || '/',
        localFolderPath: state.localPath,
        syncIntervalSeconds: state.interval,
        direction: defaultDirection(),
        folderMappings: mappings,
        enabled: true,
      }),
    });
    state.jobId = saved.jobId || state.jobId;
    els.execProgress.label = 'Inventarisatie';
    appendFeed('synced', 'Bestanden vergelijken…');
    const inventory = await api('/api/projects/inventory', {
      method: 'POST',
      body: JSON.stringify({
        projectName: state.projectName,
        projectId: state.projectId,
        remoteFolderPath: mappings[0].remoteFolderPath || '/',
        localFolderPath: state.localPath,
        folderMappings: mappings,
      }),
    });
    inventory.jobId = state.jobId;
    await api('/api/setup/activate', {
      method: 'POST',
      body: JSON.stringify({
        jobId: state.jobId,
        projectId: state.projectId,
        localFolderPath: state.localPath,
      }),
    });
    renderInventory(inventory);
    await loadConfig();
    await refreshStatus();
    showAlert('success', 'Synchronisatie is gestart.');
  } catch (error) {
    showAlert('error', error.message);
    els.invCopy.textContent = error.message;
    els.execProgress.indeterminate = false;
  } finally {
    els.invLoader.hidden = true;
  }
});

bindButton(els.settingsBtn, () => {
  setTab(5);
});
bindButton(els.addProjectBtn, startProjectWizard);
bindButton(els.activityBtn, () => {
  window.location.href = '/activity';
});

if (els.overviewStatusFilter) {
  els.overviewStatusFilter.options = OVERVIEW_STATUS_OPTIONS;
  els.overviewStatusFilter.value = state.overviewStatus;
  els.overviewStatusFilter.addEventListener('inputChange', (event) => {
    state.overviewStatus = readInputString(event) || 'all';
    els.overviewStatusFilter.value = state.overviewStatus;
    renderOverviewTable();
  });
}
if (els.overviewSearch) {
  els.overviewSearch.addEventListener('inputChange', (event) => {
    state.overviewSearch = readInputString(event);
    els.overviewSearch.value = state.overviewSearch;
    renderOverviewTable();
  });
}
if (els.detailErp) {
  els.detailErp.addEventListener('inputChange', (event) => {
    state.detailErp = readInputString(event);
    els.detailErp.value = state.detailErp;
  });
}
if (els.detailDescription) {
  els.detailDescription.addEventListener('inputChange', (event) => {
    state.detailDescription = readInputString(event);
    els.detailDescription.value = state.detailDescription;
  });
}
if (els.detailTags) {
  els.detailTags.addEventListener('inputChange', (event) => {
    state.detailTags = readInputString(event);
    els.detailTags.value = state.detailTags;
  });
}
if (els.detailInterval) {
  els.detailInterval.addEventListener('inputChange', (event) => {
    state.detailInterval = readInputString(event);
    els.detailInterval.value = state.detailInterval;
  });
}
if (els.detailWriteTags) {
  els.detailWriteTags.addEventListener('inputChange', (event) => {
    state.detailWriteTags = readInputChecked(event);
    els.detailWriteTags.value = state.detailWriteTags;
  });
}
bindButton(els.detailCancel, () => closeMappingDetail());
bindButton(els.detailSave, async () => {
  if (!state.selectedOverviewJob) {
    return;
  }
  try {
    const tags = state.detailTags.split(',').map((tag) => tag.trim()).filter(Boolean);
    await api('/api/overview/jobs/metadata', {
      method: 'PUT',
      body: JSON.stringify({
        jobId: state.selectedOverviewJob.jobId,
        erpProjectId: state.detailErp,
        description: state.detailDescription,
        tags,
        syncIntervalSeconds: Number(state.detailInterval) || 60,
        writeConnectTags: state.detailWriteTags,
      }),
    });
    closeMappingDetail();
    showAlert('success', 'Mapmetadata is opgeslagen.');
    await loadOverview();
  } catch (error) {
    showAlert('error', error.message);
  }
});

els.prefDirection.options = DEFAULT_DIRECTION_OPTIONS;
els.prefDirection.value = state.defaultDirection;
renderLocalPath();
setLinkMode('existing');
setWizardStep(0);
setSharedStep(0);
setProvisionStep(0);
renderMappingRows();

els.tabs.tabs = [
  { label: 'Overzicht', icon: 'home', iconPosition: 'left' },
  { label: 'Mappen & projecten', icon: 'folder_closed', iconPosition: 'left' },
  { label: 'Project koppelen', icon: 'folder_open', iconPosition: 'left' },
  { label: 'Centrale mappen', icon: 'link', iconPosition: 'left' },
  { label: 'Nieuw project', icon: 'add', iconPosition: 'left' },
  { label: 'Instellingen', icon: 'settings', iconPosition: 'left' },
  { label: 'Systeem & backup', icon: 'download', iconPosition: 'left' },
];
els.tabs.activeTabIndex = 0;
els.tabs.addEventListener('tabChange', (event) => {
  els.tabs.activeTabIndex = event.detail?.newTab ?? 0;
});

els.sharedName.addEventListener('inputChange', (event) => {
  state.sharedName = readInputString(event);
  els.sharedName.value = state.sharedName;
});
els.sharedFolderName.value = '99_Algemeen';
els.sharedAutocreate.value = true;
els.sharedFolderName.addEventListener('inputChange', (event) => {
  state.sharedFolderName = readInputString(event).trim() || '99_Algemeen';
  els.sharedFolderName.value = state.sharedFolderName;
});
els.sharedAutocreate.addEventListener('inputChange', (event) => {
  state.sharedAutoCreate = readInputChecked(event);
  els.sharedAutocreate.value = state.sharedAutoCreate;
});
bindButton(els.sharedNewBtn, () => {
  resetSharedForm();
});
bindButton(els.sharedBrowseBtn, () => openFolderBrowser('shared').catch((error) => showAlert('error', error.message)));
bindButton(els.sharedBack, () => setSharedStep(state.sharedStep - 1));
bindButton(els.sharedNext, () => {
  if (!sharedStepReady(state.sharedStep)) {
    return;
  }
  showAlert(null);
  setSharedStep(state.sharedStep + 1);
});
els.sharedTable.addEventListener('rowClick', (event) => {
  const row = event.detail?.row || {};
  const id = row.id ?? event.detail?.id;
  const rules = state.config?.sharedSyncRules || [];
  const rule = rules.find((item) => item.ruleId === id) || rules[Number(id)];
  if (rule) {
    loadSharedForm(rule);
  }
});
els.projectsTable.addEventListener('rowClick', async (event) => {
  const row = event.detail?.row || {};
  const jobs = state.config?.syncJobs || [];
  const job = jobs.find((item) => item.jobId === row.id) || jobs.find((item) => item.projectName === row.projectName);
  if (!job) {
    return;
  }
  state.jobId = job.jobId || '';
  state.projectId = '';
  state.projectName = '';
  state.localPath = job.localProjectRoot || job.localFolderPath || state.localPath;
  renderLocalPath();
  state.mappings = [];
  if (els.projectSelect) {
    els.projectSelect.value = '';
  }
  setTab(2);
  setWizardStep(0);
  await scanLocal().catch(() => undefined);
});
bindButton(els.sharedDeleteBtn, async () => {
  try {
    if (!state.sharedRuleId) {
      showAlert('error', 'Kies eerst een centrale map in de tabel.');
      return;
    }
    const config = state.config || await api('/api/setup/config');
    const rules = (config.sharedSyncRules || []).filter((rule) => rule.ruleId !== state.sharedRuleId);
    await saveConfig({ ...config, sharedSyncRules: rules });
    resetSharedForm();
    showAlert('success', 'Centrale map verwijderd.');
  } catch (error) {
    showAlert('error', error.message);
  }
});
bindButton(els.sharedSaveBtn, async () => {
  try {
    const folderName = (state.sharedFolderName || els.sharedFolderName.value || '99_Algemeen').replace(/^\/+|\/+$/g, '') || '99_Algemeen';
    if (!state.sharedName || !state.sharedPath) {
      showAlert('error', 'Naam en lokale bronmap zijn verplicht.');
      return;
    }
    if (!state.sharedProjectIds.length) {
      showAlert('error', 'Kies minstens één Trimble Connect-project.');
      return;
    }
    const config = state.config || await api('/api/setup/config');
    const rules = [...(config.sharedSyncRules || [])];
    const ruleId = state.sharedRuleId || (globalThis.crypto?.randomUUID?.() || `shared-${Date.now()}`);
    const nextRule = {
      ruleId,
      name: state.sharedName,
      localFolderPath: state.sharedPath,
      targetFolderName: folderName,
      targetProjectIds: [...state.sharedProjectIds],
      direction: 'LocalToCloud',
      syncIntervalSeconds: 300,
      autoCreateRemoteFolder: state.sharedAutoCreate !== false,
      syncTargets: state.sharedProjectIds.map((projectId) => ({
        projectId,
        projectName: projectById(projectId)?.name || '',
        remoteFolderPath: `/${folderName}`,
        remoteFolderId: '',
      })),
    };
    const index = rules.findIndex((rule) => rule.ruleId === ruleId || rule.name === state.sharedName);
    if (index >= 0) {
      rules[index] = nextRule;
    } else {
      rules.push(nextRule);
    }
    await saveConfig({ ...config, sharedSyncRules: rules });
    resetSharedForm();
    showAlert('success', 'Centrale map gekoppeld. Ontbrekende doelmapen worden per project aangemaakt.');
  } catch (error) {
    showAlert('error', error.message);
  }
});

els.prefDirection.addEventListener('inputChange', (event) => {
  state.defaultDirection = readInputString(event) === 'TwoWay' ? 'TwoWay' : 'LocalToCloud';
  els.prefDirection.value = state.defaultDirection;
});
els.prefRemoteParent.addEventListener('inputChange', (event) => {
  const value = readInputString(event).trim() || '/';
  state.defaultRemoteParent = value.startsWith('/') ? value : `/${value}`;
  els.prefRemoteParent.value = state.defaultRemoteParent === '/' ? '' : state.defaultRemoteParent;
});
function portNoticeText(port) {
  const safe = Number.isInteger(port) && port > 0 && port <= 65535 ? port : 5000;
  return `Let op: Na het wijzigen van de poort dient de applicatie opnieuw te worden opgestart en moet het nieuwe callback-adres (bijv. http://localhost:${safe}/callback) geregistreerd staan in de Trimble Developer Console.`;
}

function setListenPortField(port) {
  const number = Number(port);
  const safe = Number.isInteger(number) && number > 0 && number <= 65535 ? number : 5000;
  state.listenPort = String(safe);
  if (els.listenPort) {
    els.listenPort.value = state.listenPort;
  }
  if (els.portNotice) {
    els.portNotice.variant = 'warning';
    els.portNotice.alertDescription = portNoticeText(safe);
  }
}

if (els.listenPort) {
  els.listenPort.addEventListener('inputChange', (event) => {
    const value = readInputString(event).trim();
    state.listenPort = value;
    els.listenPort.value = value;
    const number = Number(value);
    if (els.portNotice) {
      els.portNotice.alertDescription = portNoticeText(number);
    }
  });
}

bindButton(els.portSave, async () => {
  const port = Number(state.listenPort);
  if (!Number.isInteger(port) || port < 1 || port > 65535) {
    showAlert('error', 'Kies een poort tussen 1 en 65535.');
    return;
  }
  try {
    const result = await api('/api/system/port', {
      method: 'PUT',
      body: JSON.stringify({ port }),
    });
    setListenPortField(result.port || port);
    showAlert('success', result.message || 'Poort opgeslagen. Herstart de connector om de nieuwe poort te gebruiken.');
  } catch (error) {
    showAlert('error', error.message);
  }
});

bindButton(els.prefSave, async () => {
  try {
    const config = state.config || await api('/api/setup/config');
    await saveConfig({
      ...config,
      wizardPreferences: {
        defaultSyncDirection: state.defaultDirection,
        defaultRemoteParentPath: state.defaultRemoteParent || '/',
      },
    });
    showAlert('success', 'Wizardvoorkeuren zijn opgeslagen.');
  } catch (error) {
    showAlert('error', error.message);
  }
});
els.templateSelect.addEventListener('inputChange', (event) => {
  state.templateId = readInputString(event);
  const project = projectById(state.templateId);
  state.templateName = project?.name || '';
  els.templateSelect.value = state.templateId;
});
els.cloneName.addEventListener('inputChange', (event) => {
  state.cloneName = readInputString(event);
  els.cloneName.value = state.cloneName;
});
els.cloneDescription.addEventListener('inputChange', (event) => {
  state.cloneDescription = readInputString(event);
  els.cloneDescription.value = state.cloneDescription;
});
els.autoProvision.addEventListener('inputChange', (event) => {
  state.autoProvision = readInputChecked(event);
  els.autoProvision.value = state.autoProvision;
});
bindButton(els.provisionSettingsBtn, async () => {
  try {
    const config = state.config || await api('/api/setup/config');
    await saveConfig({
      ...config,
      projectProvisioning: {
        ...(config.projectProvisioning || {}),
        autoProvisionOnFolderTrigger: state.autoProvision,
        triggerFileName: '_trimble_sync.json',
        defaultTemplateProjectId: state.templateId,
        defaultTemplateProjectName: state.templateName,
        defaultRegion: 'europe',
        watchRoot: state.watchRoot,
      },
    });
    showAlert('success', 'Provisioning-instellingen opgeslagen.');
  } catch (error) {
    showAlert('error', error.message);
  }
});
bindButton(els.cloneBtn, async () => {
  try {
    showAlert(null);
    const project = await api('/api/setup/provision', {
      method: 'POST',
      body: JSON.stringify({
        templateProjectId: state.templateId,
        templateProjectName: state.templateName,
        name: state.cloneName,
        description: state.cloneDescription,
        localFolderPath: state.cloneLocal || state.localPath,
      }),
    });
    showAlert('success', `Project ${project.name || 'aangemaakt'} is gekoppeld.`);
    await loadConfig();
    await loadProjects();
    await refreshStatus();
    setTab(0);
  } catch (error) {
    showAlert('error', error.message);
  }
});

const params = new URLSearchParams(window.location.search);
if (params.get('loggedIn') === '1') {
  showAlert('success', 'Authenticatie geslaagd.');
  history.replaceState({}, '', '/');
}
if (params.get('authError')) {
  showAlert('error', `Inloggen mislukt: ${params.get('authError')}`);
}

function restoreDialog() {
  return document.getElementById('restore-confirm-dialog');
}

function selectedZip(event) {
  const detail = event?.detail;
  if (!detail) {
    return null;
  }
  if (typeof detail.length === 'number') {
    return detail.length ? detail[0] : null;
  }
  if (detail.files && detail.files.length) {
    return detail.files[0];
  }
  return null;
}

function renderAuthHealth(health) {
  const active = Boolean(health?.hasRefreshToken) && health?.isAuthenticated !== false;
  if (els.authHealthBadge) {
    els.authHealthBadge.variant = 'filled';
    els.authHealthBadge.color = active ? 'success' : 'danger';
    els.authHealthBadge.textContent = active ? 'Actief' : 'Her-authenticatie vereist';
  }
  if (els.authExpiry) {
    const expiry = health?.accessTokenExpiresAt ? formatLocalTimestamp(new Date(health.accessTokenExpiresAt)) : 'nog niet ververst';
    els.authExpiry.textContent = `Access token verloopt: ${expiry}`;
  }
  if (els.authRefreshUpdated) {
    const updated = health?.refreshTokenLastUpdated ? formatLocalTimestamp(new Date(health.refreshTokenLastUpdated)) : 'nog niet opgeslagen';
    els.authRefreshUpdated.textContent = `Refresh token bijgewerkt: ${updated}`;
  }
}

function renderDiagnostics(report) {
  const hosts = (report?.hosts || []).map((host) => {
    const dns = host.dnsResolved ? 'DNS ok' : 'DNS mislukt';
    const reach = host.reachable ? host.detail : (host.detail || 'niet bereikbaar');
    return `${host.host}: ${dns}. ${reach}`;
  });
  const storage = report?.storageDetail || 'Opslagstatus onbekend.';
  if (els.authDiagAlert) {
    const healthy = (report?.hosts || []).every((host) => host.dnsResolved && host.reachable)
      && report?.dataDirectoryWritable
      && report?.databaseWritable;
    els.authDiagAlert.hidden = false;
    els.authDiagAlert.variant = healthy ? 'success' : 'warning';
    els.authDiagAlert.alertDescription = [...hosts, storage].join(' ');
  }
}

async function loadAuthHealth() {
  renderAuthHealth(await api('/api/system/auth'));
}

async function loadLanAccess() {
  const info = await api('/api/system/network');
  const urls = Array.isArray(info.urls) && info.urls.length ? info.urls : [info.localUrl || 'http://localhost:5000'];
  const primary = urls[0];
  if (els.lanLink) {
    els.lanLink.href = primary;
    els.lanLink.textContent = primary;
  }
  setListenPortField(info.port);
  if (els.lanBanner) {
    els.lanBanner.hidden = false;
    els.lanBanner.variant = info.listeningOnAllInterfaces === false ? 'warning' : 'info';
    const extra = urls.length > 1 ? ` Andere adressen: ${urls.slice(1).join(', ')}.` : '';
    els.lanBanner.alertDescription = info.listeningOnAllInterfaces === false
      ? `Dit dashboard luistert alleen op deze pc (${info.localUrl}). LAN-binding is niet actief.${extra}`
      : `Deel deze link met collega’s op hetzelfde netwerk.${extra}`;
  }
}

async function downloadBackup() {
  const response = await fetch('/api/system/backup');
  if (!response.ok) {
    const body = await response.json().catch(() => ({}));
    throw new Error(body.error || `HTTP ${response.status}`);
  }
  const blob = await response.blob();
  const header = response.headers.get('Content-Disposition') || '';
  const match = /filename="?([^";]+)"?/i.exec(header);
  const fileName = match ? match[1] : 'TrimbleConnector-Backup.zip';
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = fileName;
  document.body.append(link);
  link.click();
  link.remove();
  URL.revokeObjectURL(url);
  showAlert('success', `Backup gedownload: ${fileName}`);
}

async function restoreBackup() {
  if (!state.restoreFile) {
    showAlert('error', 'Kies eerst een .zip-backup.');
    return;
  }
  const body = new FormData();
  body.append('file', state.restoreFile, state.restoreFile.name);
  const response = await fetch('/api/system/restore', { method: 'POST', body });
  const payload = await response.json().catch(() => ({}));
  if (!response.ok) {
    throw new Error(payload.error || `HTTP ${response.status}`);
  }
  if (els.restoreAlert) {
    els.restoreAlert.hidden = false;
    els.restoreAlert.variant = 'success';
    els.restoreAlert.alertDescription = payload.message || 'Backup succesvol hersteld!';
  }
  showAlert('success', payload.message || 'Backup succesvol hersteld!');
  if (els.restoreDropzone && typeof els.restoreDropzone.reset === 'function') {
    await els.restoreDropzone.reset();
  }
  state.restoreFile = null;
  await loadConfig();
  await loadOverview();
  await refreshStatus();
}

if (els.restoreDropzone) {
  els.restoreDropzone.addEventListener('fileSelect', (event) => {
    state.restoreFile = selectedZip(event);
  });
}
bindButton(els.authLoginBtn, startLogin);
bindButton(els.authRefreshBtn, async () => {
  const result = await api('/api/system/auth/refresh', { method: 'POST' });
  if (result.health) {
    renderAuthHealth(result.health);
  }
  if (result.success === false) {
    throw new Error(result.error || 'Tokenverversing mislukt.');
  }
  showAlert('success', 'Access token is ververst.');
});
bindButton(els.authDiagBtn, async () => {
  renderDiagnostics(await api('/api/system/diagnostics', { method: 'POST' }));
});
bindButton(els.backupDownloadBtn, downloadBackup);
bindButton(els.restoreBtn, () => {
  if (!state.restoreFile) {
    showAlert('error', 'Kies eerst een .zip-backup.');
    return;
  }
  restoreDialog()?.showModal();
});
bindButton(els.restoreCancelBtn, () => {
  restoreDialog()?.close();
});
bindButton(els.restoreConfirmBtn, async () => {
  restoreDialog()?.close();
  await restoreBackup();
});

try {
  await loadConfig();
  try {
    await loadLanAccess();
    await loadAuthHealth();
  } catch (error) {
    showAlert('error', error.message);
  }
  const status = await refreshStatus();
  if (status.authenticated) {
    await loadProjects();
  }
  setTab(0);
} catch (error) {
  showAlert('error', error.message);
}

window.setInterval(() => {
  refreshStatus().catch(() => undefined);
}, 4000);
