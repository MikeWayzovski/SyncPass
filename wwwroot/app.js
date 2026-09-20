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
  remoteFolderName: '',
  sharedName: '',
  sharedPath: '',
  sharedDirection: 'LocalToCloud',
  sharedTargets: [],
  selectedTarget: -1,
  templateId: '',
  templateName: '',
  cloneName: '',
  cloneDescription: '',
  cloneLocal: '/home/jack/Projecten/Sluis',
  watchRoot: '',
  autoProvision: false,
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
  projectsTable: document.getElementById('projects-table'),
  sharedOverviewTable: document.getElementById('shared-overview-table'),
  sharedName: document.getElementById('shared-name'),
  sharedPath: document.getElementById('shared-path'),
  sharedProject: document.getElementById('shared-project'),
  sharedFolder: document.getElementById('shared-folder'),
  sharedDirection: document.getElementById('shared-direction'),
  sharedAddBtn: document.getElementById('shared-add-btn'),
  sharedRemoveBtn: document.getElementById('shared-remove-btn'),
  sharedSaveBtn: document.getElementById('shared-save-btn'),
  sharedTable: document.getElementById('shared-table'),
  templateSelect: document.getElementById('template-select'),
  cloneName: document.getElementById('clone-name'),
  cloneDescription: document.getElementById('clone-description'),
  cloneLocal: document.getElementById('clone-local'),
  watchRoot: document.getElementById('watch-root'),
  autoProvision: document.getElementById('auto-provision'),
  cloneBtn: document.getElementById('clone-btn'),
  provisionSettingsBtn: document.getElementById('provision-settings-btn'),
};

['kpi-connection', 'kpi-projects', 'kpi-shared', 'inv-upload', 'inv-download', 'inv-synced', 'summary-card'].forEach((id) => {
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

function renderSharedTable() {
  els.sharedTable.columns = [
    { id: 'projectName', header: 'Project', accessor: 'projectName' },
    { id: 'remoteFolderPath', header: 'Remote pad', accessor: 'remoteFolderPath' },
  ];
  els.sharedTable.data = state.sharedTargets.map((row, index) => ({
    id: String(index),
    projectName: row.projectName || projectById(row.projectId)?.name || 'Project',
    remoteFolderPath: row.remoteFolderPath || '/',
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
    id: String(index),
    name: rule.name,
    localFolderPath: rule.localFolderPath,
    targets: (rule.syncTargets || [])
      .map((target) => `${target.projectName || projectById(target.projectId)?.name || 'Project'} → ${target.remoteFolderPath || '/'}`)
      .join(', ') || 'Geen doelen',
  }));
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
  const shared = config.sharedSyncRules?.[0];
  if (shared) {
    state.sharedName = shared.name || '';
    state.sharedPath = shared.localFolderPath || '';
    state.sharedDirection = shared.direction || 'LocalToCloud';
    state.sharedTargets = (shared.syncTargets || []).map((row) => ({
      projectName: row.projectName || '',
      projectId: row.projectId || '',
      remoteFolderPath: row.remoteFolderPath || '/',
    }));
    els.sharedName.value = state.sharedName;
    els.sharedPath.value = state.sharedPath;
    els.sharedDirection.value = state.sharedDirection;
  }
  const provisioning = config.projectProvisioning || {};
  state.templateId = provisioning.defaultTemplateProjectId || state.templateId;
  state.templateName = provisioning.defaultTemplateProjectName || state.templateName;
  state.watchRoot = provisioning.watchRoot || '';
  state.autoProvision = Boolean(provisioning.autoProvisionOnFolderTrigger);
  els.watchRoot.value = state.watchRoot;
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

function applyAvatar(img, fallback, url, name) {
  img.onerror = () => {
    img.hidden = true;
    fallback.hidden = false;
  };
  const next = url ? (url.startsWith('http') ? '/api/setup/avatar' : url) : '';
  if (!next) {
    img.removeAttribute('src');
    img.alt = '';
    img.hidden = true;
    fallback.hidden = false;
    return;
  }
  if (img.getAttribute('src') === next && !img.hidden) {
    img.alt = name;
    return;
  }
  img.src = next;
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
    const thumbnail = status.user?.thumbnail || status.userThumbnail || '';
    els.authBadge.color = 'success';
    els.authBadge.textContent = 'Ingelogd';
    els.authUser.textContent = who;
    applyAvatar(els.authAvatar, els.authAvatarFallback, thumbnail, who);
    els.headerProfile.classList.remove('hidden');
    els.headerUserName.textContent = who;
    applyAvatar(els.headerAvatar, els.headerAvatarFallback, thumbnail, who);
    els.kpiConnectionBadge.color = 'success';
    els.kpiConnectionBadge.textContent = 'Verbonden';
    els.kpiConnectionCopy.textContent = who;
    els.loginBtn.hidden = true;
  } else {
    els.authBadge.color = undefined;
    els.authBadge.textContent = 'Niet ingelogd';
    els.authUser.textContent = '';
    applyAvatar(els.authAvatar, els.authAvatarFallback, '', '');
    els.authAvatarFallback.hidden = true;
    els.headerProfile.classList.add('hidden');
    els.headerUserName.textContent = '';
    els.kpiConnectionBadge.color = undefined;
    els.kpiConnectionBadge.textContent = 'Offline';
    els.kpiConnectionCopy.textContent = 'Log in om projecten te beheren.';
    els.loginBtn.hidden = false;
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

async function openFolderBrowser() {
  await loadBrowse(state.localPath || state.browsePath || '');
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
  state.localPath = state.browsePath;
  closeFolderBrowser();
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
  if (els.sharedProject) {
    els.sharedProject.options = options;
  }
  els.projectSelect.value = state.projectId || '';
  renderStepper();
  renderDashboard();
}

async function refreshStatus() {
  state.status = await api('/api/setup/status');
  renderAuth(state.status);
  renderLogs(state.status);
  return state.status;
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

bindButton(els.loginBtn, async () => {
  const { url } = await api('/api/setup/login-url');
  window.location.href = url;
});

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

bindButton(els.browseBtn, () => openFolderBrowser().catch((error) => showAlert('error', error.message)));
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
  setTab(4);
});
bindButton(els.addProjectBtn, () => {
  state.projectId = '';
  state.projectName = '';
  state.jobId = '';
  if (els.projectSelect) {
    els.projectSelect.value = '';
  }
  setTab(1);
  setWizardStep(0);
});
bindButton(els.activityBtn, () => {
  window.location.href = '/activity';
});

els.sharedDirection.options = DIRECTION_OPTIONS;
els.sharedDirection.value = 'LocalToCloud';
els.prefDirection.options = DEFAULT_DIRECTION_OPTIONS;
els.prefDirection.value = state.defaultDirection;
renderLocalPath();
setLinkMode('existing');
setWizardStep(0);
renderMappingRows();

els.tabs.tabs = [
  { label: 'Overzicht', icon: 'home', iconPosition: 'left' },
  { label: 'Project koppelen', icon: 'folder_closed', iconPosition: 'left' },
  { label: 'Centrale mappen', icon: 'link', iconPosition: 'left', disabled: true },
  { label: 'Nieuw project', icon: 'add', iconPosition: 'left' },
  { label: 'Instellingen', icon: 'settings', iconPosition: 'left' },
];
els.tabs.activeTabIndex = 0;
els.tabs.addEventListener('tabChange', (event) => {
  els.tabs.activeTabIndex = event.detail?.newTab ?? 0;
});

els.sharedName.addEventListener('inputChange', (event) => {
  state.sharedName = readInputString(event);
  els.sharedName.value = state.sharedName;
});
els.sharedPath.addEventListener('inputChange', (event) => {
  state.sharedPath = readInputString(event);
  els.sharedPath.value = state.sharedPath;
});
els.sharedFolder.addEventListener('inputChange', (event) => {
  els.sharedFolder.value = readInputString(event);
});
bindButton(els.sharedAddBtn, () => {
  const project = projectById(els.sharedProject.value) || projectById(state.projectId);
  const remoteFolderPath = els.sharedFolder.value || '/';
  if (!project || !remoteFolderPath) {
    showAlert('error', 'Kies een doelproject en een remote map-pad.');
    return;
  }
  state.sharedTargets.push({
    projectName: project.name,
    projectId: project.id,
    remoteFolderPath,
  });
  renderSharedTable();
});
bindButton(els.sharedRemoveBtn, () => {
  if (state.selectedTarget >= 0) {
    state.sharedTargets.splice(state.selectedTarget, 1);
    state.selectedTarget = -1;
    renderSharedTable();
  }
});
els.sharedTable.addEventListener('rowClick', (event) => {
  const row = event.detail?.row || {};
  state.selectedTarget = Number(row.id ?? event.detail?.id);
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
  setTab(1);
  setWizardStep(0);
  await scanLocal().catch(() => undefined);
});
bindButton(els.sharedSaveBtn, async () => {
  try {
    if (!state.sharedName || !state.sharedPath) {
      showAlert('error', 'Naam en lokale map zijn verplicht.');
      return;
    }
    const config = state.config || await api('/api/setup/config');
    const rules = [...(config.sharedSyncRules || [])];
    const nextRule = {
      name: state.sharedName,
      localFolderPath: state.sharedPath,
      direction: els.sharedDirection.value || 'LocalToCloud',
      syncIntervalSeconds: 300,
      syncTargets: state.sharedTargets,
    };
    const index = rules.findIndex((rule) => rule.name === state.sharedName);
    if (index >= 0) {
      rules[index] = nextRule;
    } else {
      rules.push(nextRule);
    }
    await saveConfig({ ...config, sharedSyncRules: rules });
    showAlert('success', 'Centrale map opgeslagen. Paden worden per project aangemaakt indien nodig.');
    setTab(0);
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
els.cloneLocal.addEventListener('inputChange', (event) => {
  state.cloneLocal = readInputString(event);
  els.cloneLocal.value = state.cloneLocal;
});
els.watchRoot.addEventListener('inputChange', (event) => {
  state.watchRoot = readInputString(event);
  els.watchRoot.value = state.watchRoot;
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

try {
  await loadConfig();
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
