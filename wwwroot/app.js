import { defineCustomElements } from 'https://cdn.jsdelivr.net/npm/@trimble-oss/moduswebcomponents@1.17.0/loader/index.js';

await defineCustomElements(undefined, {
  resourcesUrl: 'https://cdn.jsdelivr.net/npm/@trimble-oss/moduswebcomponents@1.17.0/dist/',
});

const STEP_LABELS = ['Inloggen', 'Project', 'Cloudmap', 'Lokale map', 'Starten'];

const DIRECTION_OPTIONS = [
  { label: 'Twee richtingen', value: 'TwoWay' },
  { label: 'Lokaal naar cloud', value: 'LocalToCloud' },
  { label: 'Cloud naar lokaal', value: 'CloudToLocal' },
];

const state = {
  status: null,
  config: null,
  projects: [],
  projectId: '',
  rootId: '',
  folderId: '',
  folderName: 'Hoofdmap',
  crumbs: [],
  localPath: '/home/jack/TestSync',
  direction: 'TwoWay',
  interval: 60,
  step: 0,
  hydrated: false,
  mappings: [],
  selectedMapping: -1,
  sharedName: '',
  sharedPath: '',
  sharedDirection: 'LocalToCloud',
  sharedTargets: [],
  selectedTarget: -1,
  templateId: '',
  cloneName: '',
  cloneDescription: '',
  cloneLocal: '/home/jack/TestSync',
  watchRoot: '',
  autoProvision: false,
};

const els = {
  stepper: document.getElementById('stepper'),
  alert: document.getElementById('page-alert'),
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
  direction: document.getElementById('direction-select'),
  slider: document.getElementById('interval-slider'),
  intervalLabel: document.getElementById('interval-label'),
  saveBtn: document.getElementById('save-btn'),
  settingsBtn: document.getElementById('settings-btn'),
  activityBtn: document.getElementById('activity-btn'),
  wizard: document.getElementById('wizard'),
  jobBadge: document.getElementById('job-badge'),
  jobSummary: document.getElementById('job-summary'),
  logs: document.getElementById('log-list'),
  tabs: document.getElementById('main-tabs'),
  mapSubpath: document.getElementById('map-subpath'),
  mapRemote: document.getElementById('map-remote'),
  mapDirection: document.getElementById('map-direction'),
  mapAddBtn: document.getElementById('map-add-btn'),
  mapRemoveBtn: document.getElementById('map-remove-btn'),
  mapSaveBtn: document.getElementById('map-save-btn'),
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

function readInputString(event) {
  return event.detail?.target?.value ?? '';
}

function bindButton(element, handler) {
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

function readInputChecked(event) {
  return Boolean(event.detail?.target?.checked);
}

function currentJobFromConfig() {
  const jobs = state.config?.syncJobs || [];
  return jobs.find((job) => job.projectId === state.projectId) || jobs[0] || null;
}

function renderMappingTable() {
  els.mapTable.columns = [
    { id: 'localSubPath', header: 'Lokale submap', accessor: 'localSubPath' },
    { id: 'remoteFolderId', header: 'Remote map', accessor: 'remoteFolderId' },
    { id: 'direction', header: 'Richting', accessor: 'direction' },
  ];
  els.mapTable.data = state.mappings.map((row, index) => ({
    id: String(index),
    localSubPath: row.localSubPath || '(projectroot)',
    remoteFolderId: row.remoteFolderId,
    direction: row.direction,
  }));
}

function renderSharedTable() {
  els.sharedTable.columns = [
    { id: 'projectId', header: 'Project', accessor: 'projectId' },
    { id: 'remoteFolderId', header: 'Remote map', accessor: 'remoteFolderId' },
  ];
  els.sharedTable.data = state.sharedTargets.map((row, index) => ({
    id: String(index),
    projectId: row.projectId,
    remoteFolderId: row.remoteFolderId,
  }));
}

function applyConfig(config) {
  state.config = config;
  const job = currentJobFromConfig();
  if (job) {
    state.mappings = (job.folderMappings || []).map((row) => ({
      localSubPath: row.localSubPath || '',
      remoteFolderId: row.remoteFolderId,
      direction: row.direction || 'TwoWay',
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
    state.sharedTargets = (shared.syncTargets || []).map((row) => ({ ...row }));
    els.sharedName.value = state.sharedName;
    els.sharedPath.value = state.sharedPath;
    els.sharedDirection.value = state.sharedDirection;
  }
  const provisioning = config.projectProvisioning || {};
  state.templateId = provisioning.defaultTemplateProjectId || state.templateId;
  state.watchRoot = provisioning.watchRoot || '';
  state.autoProvision = Boolean(provisioning.autoProvisionOnFolderTrigger);
  els.watchRoot.value = state.watchRoot;
  els.autoProvision.value = state.autoProvision;
  if (state.templateId) {
    els.templateSelect.value = state.templateId;
  }
  renderMappingTable();
  renderSharedTable();
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

function projectOptions() {
  return [
    { label: 'Kies een project', value: '', hidden: true },
    ...state.projects.map((project) => ({
      label: project.location ? `${project.name} (${project.location})` : project.name,
      value: project.id,
    })),
  ];
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

function setSteps(active) {
  state.step = active;
  els.stepper.steps = STEP_LABELS.map((label, index) => ({
    label,
    color: index <= active ? 'primary' : 'neutral',
  }));
}

function applyJobToForm(job) {
  if (!job) {
    return;
  }
  state.projectId = job.projectId || state.projectId;
  state.folderId = job.remoteFolderId || state.folderId;
  state.localPath = job.localFolderPath || state.localPath;
  state.direction = job.direction || 'TwoWay';
  state.interval = job.syncIntervalSeconds || 60;
  els.localPath.value = state.localPath;
  els.direction.value = state.direction;
  els.slider.value = state.interval;
  els.intervalLabel.textContent = `${state.interval} seconden`;
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
  } else {
    els.authBadge.color = undefined;
    els.authBadge.textContent = 'Niet ingelogd';
    els.authUser.textContent = '';
    applyAvatar(els.authAvatar, els.authAvatarFallback, '', '');
    els.authAvatarFallback.hidden = true;
    els.headerProfile.classList.add('hidden');
    els.headerUserName.textContent = '';
    applyAvatar(els.headerAvatar, els.headerAvatarFallback, '', '');
  }
}

function renderLogs(status) {
  const jobs = status.jobs || [];
  const job = jobs[0];
  if (job) {
    els.jobBadge.variant = 'filled';
    els.jobBadge.color = job.state === 'error' ? 'danger' : job.state === 'syncing' ? 'primary' : 'success';
    els.jobBadge.textContent = job.state;
    els.jobSummary.textContent = jobs.length > 1
      ? `${jobs.length} sync-taken, o.a. ${job.projectId} → ${job.localFolderPath}`
      : `${job.projectId} → ${job.localFolderPath}`;
  }
  els.logs.replaceChildren();
  const lines = status.logs?.length ? status.logs : ['Nog geen logs.'];
  for (const line of lines.slice(-40)) {
    const item = document.createElement('li');
    item.textContent = line;
    els.logs.append(item);
  }
}

function renderFolders(folders) {
  els.folderList.replaceChildren();
  if (!folders.length) {
    const empty = document.createElement('modus-wc-typography');
    empty.size = 'sm';
    empty.textContent = 'Geen submappen. De huidige map wordt gebruikt.';
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
    const row = document.createElement('div');
    row.className = 'folder-row';
    row.append(button);
    els.folderList.append(row);
  }

  els.folderCrumb.textContent = state.crumbs.map((item) => item.name).join(' / ') || 'Hoofdmap';
  els.folderSelected.textContent = `Geselecteerd: ${state.folderName} (${state.folderId})`;
  if (state.folderId) {
    els.mapRemote.value = state.folderId;
    els.sharedFolder.value = state.folderId;
  }
}

async function loadFolders(folderId) {
  if (!state.projectId) {
    return;
  }
  const query = new URLSearchParams({ projectId: state.projectId });
  if (folderId) {
    query.set('folderId', folderId);
  }
  const folders = await api(`/api/setup/folders?${query}`);
  renderFolders(folders);
}

async function openFolder(folder) {
  state.folderId = folder.id;
  state.folderName = folder.name;
  state.crumbs.push({ id: folder.id, name: folder.name });
  await loadFolders(folder.id);
}

async function loadProjects() {
  state.projects = await api('/api/setup/projects');
  const options = projectOptions();
  els.projectSelect.options = options;
  els.templateSelect.options = options;
  els.sharedProject.options = options;

  const preferred = state.projectId || state.templateId || 'ihD9QVs9nyU';
  const match = state.projects.find((project) => project.id === preferred) || state.projects[0];
  if (match) {
    state.projectId = match.id;
    state.rootId = match.rootId || '';
    if (!state.folderId) {
      state.folderId = match.rootId || '';
      state.folderName = 'Hoofdmap';
      state.crumbs = [{ id: state.folderId, name: 'Hoofdmap' }];
    }
    els.projectSelect.value = match.id;
    if (!state.templateId) {
      state.templateId = match.id;
    }
    els.templateSelect.value = state.templateId;
    await loadFolders(state.folderId || match.rootId);
  }
}

async function refreshStatus() {
  state.status = await api('/api/setup/status');
  renderAuth(state.status);
  renderLogs(state.status);
  if (state.status.jobs?.[0] && !state.hydrated) {
    applyJobToForm(state.status.jobs[0]);
    state.hydrated = true;
  }
  if (state.status.authenticated && state.step < 1) {
    setSteps(1);
  }
  return state.status;
}

els.loginBtn.addEventListener('buttonClick', async () => {
  const { url } = await api('/api/setup/login-url');
  window.location.href = url;
});

els.projectSelect.addEventListener('inputChange', async (event) => {
  const value = readInputString(event);
  const project = state.projects.find((item) => item.id === value);
  if (!project) {
    return;
  }
  state.projectId = project.id;
  state.rootId = project.rootId || '';
  state.folderId = project.rootId || '';
  state.folderName = 'Hoofdmap';
  state.crumbs = [{ id: state.folderId, name: 'Hoofdmap' }];
  els.projectSelect.value = value;
  setSteps(2);
  await loadFolders(state.folderId);
});

els.folderUp.addEventListener('buttonClick', async () => {
  if (state.crumbs.length <= 1) {
    return;
  }
  state.crumbs.pop();
  const parent = state.crumbs[state.crumbs.length - 1];
  state.folderId = parent.id;
  state.folderName = parent.name;
  await loadFolders(parent.id);
});

els.localPath.addEventListener('inputChange', (event) => {
  state.localPath = readInputString(event);
  els.localPath.value = state.localPath;
});

els.direction.addEventListener('inputChange', (event) => {
  state.direction = readInputString(event) || 'TwoWay';
  els.direction.value = state.direction;
});

els.slider.addEventListener('inputChange', (event) => {
  const raw = readInputString(event);
  state.interval = Number(raw) || 60;
  els.slider.value = state.interval;
  els.intervalLabel.textContent = `${state.interval} seconden`;
});

els.saveBtn.addEventListener('buttonClick', async () => {
  try {
    showAlert(null);
    await api('/api/setup/save', {
      method: 'POST',
      body: JSON.stringify({
        projectId: state.projectId,
        remoteFolderId: state.folderId,
        localFolderPath: state.localPath,
        syncIntervalSeconds: state.interval,
        direction: state.direction,
        folderMappings: state.mappings.length
          ? state.mappings
          : [{ localSubPath: '', remoteFolderId: state.folderId, direction: state.direction }],
      }),
    });
    await loadConfig();
    setSteps(4);
    showAlert('success', 'Configuratie opgeslagen. Synchronisatie is gestart.');
    await refreshStatus();
  } catch (error) {
    showAlert('error', error.message);
  }
});

els.settingsBtn.addEventListener('buttonClick', () => {
  els.wizard.classList.remove('hidden');
  els.wizard.scrollIntoView({ behavior: 'smooth', block: 'start' });
});

els.activityBtn.addEventListener('buttonClick', () => {
  window.location.href = '/activity';
});
els.activityBtn.addEventListener('click', () => {
  window.location.href = '/activity';
});

els.stepper.addEventListener('stepClick', (event) => {
  const index = event.detail?.index ?? 0;
  setSteps(index);
  const cards = ['step-auth', 'step-project', 'step-folder', 'step-local'];
  const target = document.getElementById(cards[Math.min(index, cards.length - 1)]);
  target?.scrollIntoView({ behavior: 'smooth', block: 'start' });
});

els.direction.options = DIRECTION_OPTIONS;
els.direction.value = 'TwoWay';
els.mapDirection.options = DIRECTION_OPTIONS;
els.mapDirection.value = 'TwoWay';
els.sharedDirection.options = DIRECTION_OPTIONS;
els.sharedDirection.value = 'LocalToCloud';
els.slider.value = 60;
setSteps(0);

els.tabs.tabs = [
  { label: 'Koppeling', icon: 'folder_closed', iconPosition: 'left' },
  { label: 'Submappen', icon: 'copy', iconPosition: 'left' },
  { label: 'Centrale mappen', icon: 'share', iconPosition: 'left' },
  { label: 'Nieuw project', icon: 'add', iconPosition: 'left' },
];
els.tabs.activeTabIndex = 0;
els.tabs.addEventListener('tabChange', (event) => {
  els.tabs.activeTabIndex = event.detail?.newTab ?? 0;
});

els.mapSubpath.addEventListener('inputChange', (event) => {
  els.mapSubpath.value = readInputString(event);
});
els.mapRemote.addEventListener('inputChange', (event) => {
  els.mapRemote.value = readInputString(event);
});
bindButton(els.mapAddBtn, () => {
  const remoteFolderId = els.mapRemote.value || state.folderId;
  if (!remoteFolderId) {
    showAlert('error', 'Kies eerst een cloudmap of vul een remote folder-id in.');
    return;
  }
  state.mappings.push({
    localSubPath: els.mapSubpath.value || '',
    remoteFolderId,
    direction: els.mapDirection.value || 'TwoWay',
  });
  renderMappingTable();
});
bindButton(els.mapRemoveBtn, () => {
  if (state.selectedMapping >= 0) {
    state.mappings.splice(state.selectedMapping, 1);
    state.selectedMapping = -1;
    renderMappingTable();
  }
});
els.mapTable.addEventListener('rowClick', (event) => {
  const id = event.detail?.id ?? event.detail?.row?.id;
  state.selectedMapping = Number(id);
});
bindButton(els.mapSaveBtn, async () => {
  try {
    const config = state.config || await api('/api/setup/config');
    const jobs = [...(config.syncJobs || [])];
    const index = jobs.findIndex((job) => job.projectId === state.projectId);
    const nextJob = {
      ...(index >= 0 ? jobs[index] : {}),
      projectId: state.projectId,
      localProjectRoot: state.localPath,
      localFolderPath: state.localPath,
      remoteFolderId: state.mappings[0]?.remoteFolderId || state.folderId,
      direction: state.direction,
      syncIntervalSeconds: state.interval,
      folderMappings: state.mappings.length
        ? state.mappings
        : [{ localSubPath: '', remoteFolderId: state.folderId, direction: state.direction }],
    };
    if (index >= 0) {
      jobs[index] = nextJob;
    } else {
      jobs.push(nextJob);
    }
    await saveConfig({ ...config, syncJobs: jobs });
    showAlert('success', 'Subfolder-koppelingen opgeslagen.');
  } catch (error) {
    showAlert('error', error.message);
  }
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
  const projectId = els.sharedProject.value || state.projectId;
  const remoteFolderId = els.sharedFolder.value || state.folderId;
  if (!projectId || !remoteFolderId) {
    showAlert('error', 'Kies een doelproject en remote folder-id.');
    return;
  }
  state.sharedTargets.push({ projectId, remoteFolderId });
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
  const id = event.detail?.id ?? event.detail?.row?.id;
  state.selectedTarget = Number(id);
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
    showAlert('success', 'Centrale map opgeslagen. Wijzigingen gaan naar alle doelprojecten.');
  } catch (error) {
    showAlert('error', error.message);
  }
});

els.templateSelect.addEventListener('inputChange', (event) => {
  state.templateId = readInputString(event);
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
        name: state.cloneName,
        description: state.cloneDescription,
        localFolderPath: state.cloneLocal || state.localPath,
      }),
    });
    showAlert('success', `Project ${project.name || project.id} aangemaakt en gekoppeld.`);
    await loadConfig();
    await loadProjects();
    await refreshStatus();
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
    setSteps(status.configured ? 4 : 2);
  }
} catch (error) {
  showAlert('error', error.message);
}

window.setInterval(() => {
  refreshStatus().catch(() => undefined);
}, 4000);
