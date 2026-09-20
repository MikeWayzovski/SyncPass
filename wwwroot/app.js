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
  { label: 'Mappen' },
  { label: 'Inventaris' },
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
  crumbs: [],
  localPath: '/home/jack/Tester2',
  localExists: false,
  localFolders: [],
  linkMode: 'existing',
  interval: 60,
  mappings: [],
  selectedMapping: 0,
  jobId: '',
  inventory: null,
  wizardStep: 0,
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
  folderList: document.getElementById('folder-list'),
  folderCrumb: document.getElementById('folder-crumb'),
  folderSelected: document.getElementById('folder-selected'),
  folderUp: document.getElementById('folder-up-btn'),
  localPath: document.getElementById('local-path'),
  localScanCopy: document.getElementById('local-scan-copy'),
  localTreeTable: document.getElementById('local-tree-table'),
  stepper: document.getElementById('link-stepper'),
  wizardHeading: document.getElementById('wizard-heading'),
  wizardBack: document.getElementById('wizard-back-btn'),
  wizardNext: document.getElementById('wizard-next-btn'),
  saveBtn: document.getElementById('save-btn'),
  activateBtn: document.getElementById('activate-btn'),
  linkExisting: document.getElementById('link-existing'),
  linkNew: document.getElementById('link-new'),
  existingFields: document.getElementById('existing-project-fields'),
  newFields: document.getElementById('new-project-fields'),
  wizardCloneName: document.getElementById('wizard-clone-name'),
  wizardTemplate: document.getElementById('wizard-template-select'),
  wizardCreateBtn: document.getElementById('wizard-create-btn'),
  mapTwoWay: document.getElementById('map-twoway'),
  invUpload: document.getElementById('inv-upload-count'),
  invDownload: document.getElementById('inv-download-count'),
  invSynced: document.getElementById('inv-synced-count'),
  invLoader: document.getElementById('inv-loader'),
  invCopy: document.getElementById('inv-copy'),
  invTable: document.getElementById('inv-table'),
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
  mapTable: document.getElementById('map-table'),
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

['kpi-connection', 'kpi-projects', 'kpi-shared', 'inv-upload', 'inv-download', 'inv-synced'].forEach((id) => {
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
    if (lock) {
      return;
    }
    lock = true;
    Promise.resolve().then(() => {
      lock = false;
    });
    handler(event);
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
    { label: 'Kies een project', value: '', hidden: true },
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
  els.wizardNext.hidden = state.wizardStep === 3;
  els.saveBtn.hidden = state.wizardStep !== 3;
  els.activateBtn.hidden = state.wizardStep !== 3 || !state.inventory;
  els.saveBtn.color = state.inventory ? 'tertiary' : 'primary';
  els.saveBtn.variant = state.inventory ? 'outlined' : 'filled';
}

function setWizardStep(index) {
  state.wizardStep = Math.max(0, Math.min(3, index));
  renderStepper();
}

function renderLocalTree(folders) {
  els.localTreeTable.columns = [
    { id: 'name', header: 'Map', accessor: 'name' },
    { id: 'relativePath', header: 'Relatief pad', accessor: 'relativePath' },
    { id: 'fileCount', header: 'Bestanden', accessor: 'fileCount' },
  ];
  els.localTreeTable.data = (folders || []).map((row, index) => ({
    id: String(index),
    name: row.name || '(projectroot)',
    relativePath: row.relativePath || '(root)',
    fileCount: String(row.fileCount ?? 0),
  }));
}

function renderMappingTable() {
  els.mapTable.columns = [
    { id: 'localSubPath', header: 'Lokale map', accessor: 'localSubPath' },
    { id: 'remoteFolderPath', header: 'Remote pad', accessor: 'remoteFolderPath' },
    { id: 'direction', header: 'Richting', accessor: 'direction' },
  ];
  els.mapTable.data = state.mappings.map((row, index) => ({
    id: String(index),
    localSubPath: row.localSubPath || '(projectroot)',
    remoteFolderPath: row.remoteFolderPath || '/',
    direction: directionLabel(row.direction),
  }));
  const selected = state.mappings[state.selectedMapping];
  els.mapTwoWay.value = selected?.direction === 'TwoWay';
}

function mappingsFromLocalScan() {
  const folders = state.localFolders.length
    ? state.localFolders
    : [{ name: PathName(state.localPath), relativePath: '', fileCount: 0 }];
  state.mappings = folders.map((folder) => ({
    localSubPath: folder.relativePath || '',
    remoteFolderPath: joinRemote(state.folderPath, folder.relativePath || ''),
    direction: 'LocalToCloud',
  }));
  state.selectedMapping = 0;
  renderMappingTable();
}

function PathName(path) {
  return (path || '').split(/[\\/]/).filter(Boolean).at(-1) || path;
}

function inventoryActionCell(value) {
  const label = String(value || '');
  const wrap = document.createElement('span');
  wrap.className = 'status-row';
  const icon = document.createElement('modus-wc-icon');
  icon.size = 'xs';
  icon.decorative = true;
  const lower = label.toLowerCase();
  icon.name = lower.includes('download') ? 'download' : lower.includes('upload') ? 'upload' : 'check_circle';
  wrap.append(icon, document.createTextNode(label));
  return wrap;
}

function renderInventory(result) {
  state.inventory = result;
  els.invUpload.textContent = String(result.uploadCount ?? 0);
  els.invDownload.textContent = String(result.downloadCount ?? 0);
  els.invSynced.textContent = String(result.syncedCount ?? 0);
  els.invCopy.textContent = `${result.uploadCount} upload, ${result.downloadCount} download, ${result.syncedCount} al gelijk.`;
  els.invTable.columns = [
    { id: 'relativePath', header: 'Bestand', accessor: 'relativePath' },
    { id: 'folder', header: 'Map', accessor: 'folder' },
    { id: 'label', header: 'Actie', accessor: 'label', cellRenderer: inventoryActionCell },
  ];
  els.invTable.data = (result.items || []).map((row, index) => ({
    id: String(index),
    relativePath: row.relativePath,
    folder: row.folder,
    label: row.label,
    action: row.action,
  }));
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
  const jobs = config.syncJobs || [];
  const job = currentJobFromConfig() || jobs[0] || null;
  if (job) {
    state.jobId = job.jobId || state.jobId;
    state.projectId = job.projectId || state.projectId;
    state.projectName = job.projectName || state.projectName;
    state.mappings = (job.folderMappings || []).map((row) => ({
      localSubPath: row.localSubPath || '',
      remoteFolderPath: row.remoteFolderPath || '/',
      remoteFolderId: row.remoteFolderId || '',
      direction: row.direction || 'LocalToCloud',
    }));
    if (job.localProjectRoot || job.localFolderPath) {
      state.localPath = job.localProjectRoot || job.localFolderPath;
      els.localPath.value = state.localPath;
    }
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
  renderMappingTable();
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

  els.folderCrumb.textContent = state.folderPath || '/';
  els.folderSelected.textContent = `Remote root: ${state.folderPath || '/'}`;
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
  const result = await api(`/api/setup/local-tree?path=${encodeURIComponent(state.localPath)}`);
  state.localExists = Boolean(result.exists);
  state.localFolders = result.folders || [];
  els.localScanCopy.textContent = result.exists
    ? `${result.folders?.length || 0} mappen gevonden in ${result.path}`
    : (result.error || 'Map niet gevonden.');
  renderLocalTree(state.localFolders);
  return result;
}

function preselectProjectFromFolder() {
  const name = PathName(state.localPath);
  const match = state.projects.find((project) => project.name === name)
    || state.projects.find((project) => project.name?.toLowerCase().includes(name.toLowerCase()));
  if (match) {
    state.projectId = match.id;
    state.projectName = match.name;
    els.projectSelect.value = match.id;
  }
}

function setLinkMode(mode) {
  state.linkMode = mode;
  els.linkExisting.value = mode === 'existing';
  els.linkNew.value = mode === 'new';
  const hideExisting = mode !== 'existing';
  const hideNew = mode !== 'new';
  els.existingFields.classList.toggle('hidden', hideExisting);
  els.existingFields.toggleAttribute('hidden', hideExisting);
  els.newFields.classList.toggle('hidden', hideNew);
  els.newFields.toggleAttribute('hidden', hideNew);
}

async function loadProjects() {
  state.projects = await api('/api/setup/projects');
  const options = projectOptions();
  els.projectSelect.options = options;
  els.templateSelect.options = options;
  els.wizardTemplate.options = options;
  els.sharedProject.options = options;

  const preferred = state.projectId
    || state.projects.find((item) => item.name === state.projectName)?.id
    || state.templateId;
  const match = state.projects.find((project) => project.id === preferred)
    || state.projects.find((project) => project.name === state.projectName)
    || state.projects[0];
  if (match) {
    state.projectId = match.id;
    state.projectName = match.name;
    state.rootId = match.rootId || '';
    if (!state.folderId) {
      state.folderId = match.rootId || '';
      state.folderPath = '/';
      state.crumbs = [{ id: state.folderId, name: match.name, path: '/' }];
    }
    els.projectSelect.value = match.id;
    if (!state.templateId) {
      state.templateId = match.id;
      state.templateName = match.name;
    }
    els.templateSelect.value = state.templateId;
    els.wizardTemplate.value = state.templateId;
    await loadFolders(state.folderId || match.rootId, state.folderPath || '/');
  }
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
    const result = await scanLocal();
    if (!result.exists) {
      showAlert('error', result.error || 'Kies een bestaande lokale map.');
      return false;
    }
    showAlert(null);
    return true;
  }
  if (index === 1) {
    if (!state.projectId && !state.projectName) {
      showAlert('error', 'Kies of maak een Trimble Connect-project.');
      return false;
    }
    showAlert(null);
    mappingsFromLocalScan();
    return true;
  }
  if (index === 2) {
    if (!state.mappings.length) {
      showAlert('error', 'Geen lokale mappen om te koppelen.');
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
    return;
  }
  state.projectId = project.id;
  state.projectName = project.name;
  state.rootId = project.rootId || '';
  state.folderId = project.rootId || '';
  state.folderPath = '/';
  state.crumbs = [{ id: state.folderId, name: project.name, path: '/' }];
  els.projectSelect.value = value;
  await loadFolders(state.folderId, '/');
});

bindButton(els.folderUp, async () => {
  if (state.crumbs.length <= 1) {
    state.folderPath = '/';
    els.folderCrumb.textContent = '/';
    els.folderSelected.textContent = 'Remote root: /';
    await loadFolders(state.rootId, '/');
    return;
  }
  state.crumbs.pop();
  const parent = state.crumbs[state.crumbs.length - 1];
  state.folderId = parent.id;
  state.folderPath = parent.path || '/';
  await loadFolders(parent.id, parent.path || '/');
});

els.localPath.addEventListener('inputChange', (event) => {
  state.localPath = readInputString(event);
  els.localPath.value = state.localPath;
  state.localExists = false;
});

bindButton(els.wizardBack, () => setWizardStep(state.wizardStep - 1));
bindButton(els.wizardNext, async () => {
  try {
    if (await validateStep(state.wizardStep)) {
      if (state.wizardStep === 0) {
        preselectProjectFromFolder();
      }
      setWizardStep(state.wizardStep + 1);
    }
  } catch (error) {
    showAlert('error', error.message);
  }
});

els.linkExisting.addEventListener('inputChange', (event) => {
  if (readInputChecked(event)) {
    setLinkMode('existing');
  }
});
els.linkNew.addEventListener('inputChange', (event) => {
  if (readInputChecked(event)) {
    setLinkMode('new');
  }
});
els.wizardCloneName.addEventListener('inputChange', (event) => {
  state.cloneName = readInputString(event);
  els.wizardCloneName.value = state.cloneName;
});
els.wizardTemplate.addEventListener('inputChange', (event) => {
  state.templateId = readInputString(event);
  state.templateName = projectById(state.templateId)?.name || '';
  els.wizardTemplate.value = state.templateId;
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
  } catch (error) {
    showAlert('error', error.message);
  }
});

els.mapTable.addEventListener('rowClick', (event) => {
  const row = event.detail?.row || {};
  state.selectedMapping = Number(row.id ?? event.detail?.id);
  const selected = state.mappings[state.selectedMapping];
  els.mapTwoWay.value = selected?.direction === 'TwoWay';
});
els.mapTwoWay.addEventListener('inputChange', (event) => {
  const twoWay = readInputChecked(event);
  els.mapTwoWay.value = twoWay;
  if (state.mappings[state.selectedMapping]) {
    state.mappings[state.selectedMapping].direction = twoWay ? 'TwoWay' : 'LocalToCloud';
    renderMappingTable();
  }
});

bindButton(els.saveBtn, async () => {
  try {
    showAlert(null);
    els.invLoader.hidden = false;
    els.invCopy.textContent = 'Bezig met inventariseren…';
    const mappings = state.mappings.length
      ? state.mappings
      : [{ localSubPath: '', remoteFolderPath: state.folderPath || '/', direction: 'LocalToCloud' }];
    const saved = await api('/api/setup/save', {
      method: 'POST',
      body: JSON.stringify({
        projectName: state.projectName,
        projectId: state.projectId,
        remoteFolderPath: mappings[0].remoteFolderPath || '/',
        localFolderPath: state.localPath,
        syncIntervalSeconds: state.interval,
        direction: 'LocalToCloud',
        folderMappings: mappings,
        enabled: false,
      }),
    });
    state.jobId = saved.jobId || state.jobId;
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
    renderInventory(inventory);
    await loadConfig();
    await refreshStatus();
    showAlert('success', 'Koppeling opgeslagen. Controleer de inventaris en start daarna de sync.');
  } catch (error) {
    showAlert('error', error.message);
  } finally {
    els.invLoader.hidden = true;
  }
});

bindButton(els.activateBtn, async () => {
  try {
    await api('/api/setup/activate', {
      method: 'POST',
      body: JSON.stringify({
        jobId: state.jobId,
        projectId: state.projectId,
        localFolderPath: state.localPath,
      }),
    });
    showAlert('success', 'Achtergrondsynchronisatie is gestart.');
    await loadConfig();
    await refreshStatus();
    setTab(0);
  } catch (error) {
    showAlert('error', error.message);
  }
});

bindButton(els.settingsBtn, () => {
  setTab(1);
  setWizardStep(0);
});
bindButton(els.addProjectBtn, () => {
  setTab(1);
  setWizardStep(0);
});
bindButton(els.activityBtn, () => {
  window.location.href = '/activity';
});

els.sharedDirection.options = DIRECTION_OPTIONS;
els.sharedDirection.value = 'LocalToCloud';
els.localPath.value = state.localPath;
setLinkMode('existing');
setWizardStep(0);

els.tabs.tabs = [
  { label: 'Overzicht', icon: 'home', iconPosition: 'left' },
  { label: 'Project koppelen', icon: 'folder_closed', iconPosition: 'left' },
  { label: 'Centrale mappen', icon: 'link', iconPosition: 'left' },
  { label: 'Nieuw project', icon: 'add', iconPosition: 'left' },
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
  state.projectId = job.projectId || '';
  state.projectName = job.projectName || '';
  state.localPath = job.localProjectRoot || job.localFolderPath || state.localPath;
  els.localPath.value = state.localPath;
  state.mappings = (job.folderMappings || []).map((item) => ({
    localSubPath: item.localSubPath || '',
    remoteFolderPath: item.remoteFolderPath || '/',
    remoteFolderId: item.remoteFolderId || '',
    direction: item.direction || 'LocalToCloud',
  }));
  renderMappingTable();
  const project = projectById(state.projectId) || state.projects.find((item) => item.name === state.projectName);
  if (project) {
    state.projectId = project.id;
    state.projectName = project.name;
    state.rootId = project.rootId || '';
    state.folderId = project.rootId || '';
    state.folderPath = '/';
    state.crumbs = [{ id: state.folderId, name: project.name, path: '/' }];
    els.projectSelect.value = project.id;
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
