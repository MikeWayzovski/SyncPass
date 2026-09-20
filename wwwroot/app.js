import { defineCustomElements } from 'https://cdn.jsdelivr.net/npm/@trimble-oss/moduswebcomponents@1.17.0/loader/index.js';

await defineCustomElements(undefined, {
  resourcesUrl: 'https://cdn.jsdelivr.net/npm/@trimble-oss/moduswebcomponents@1.17.0/dist/',
});

const STEP_LABELS = ['Inloggen', 'Project', 'Cloudmap', 'Lokale map', 'Starten'];

const state = {
  status: null,
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
  wizard: document.getElementById('wizard'),
  jobBadge: document.getElementById('job-badge'),
  jobSummary: document.getElementById('job-summary'),
  logs: document.getElementById('log-list'),
};

function readInputString(event) {
  return event.detail?.target?.value ?? '';
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
  if (url) {
    img.src = url.startsWith('http') ? '/api/setup/avatar' : url;
    img.alt = name;
    img.hidden = false;
    fallback.hidden = true;
  } else {
    img.removeAttribute('src');
    img.alt = '';
    img.hidden = true;
    fallback.hidden = false;
  }
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
  const job = status.jobs?.[0];
  if (job) {
    els.jobBadge.variant = 'filled';
    els.jobBadge.color = job.state === 'error' ? 'danger' : job.state === 'syncing' ? 'primary' : 'success';
    els.jobBadge.textContent = job.state;
    els.jobSummary.textContent = `${job.projectId} → ${job.localFolderPath}`;
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
  els.projectSelect.options = [
    { label: 'Kies een project', value: '', hidden: true },
    ...state.projects.map((project) => ({
      label: project.location ? `${project.name} (${project.location})` : project.name,
      value: project.id,
    })),
  ];

  const preferred = state.projectId || 'ihD9QVs9nyU';
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
      }),
    });
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

els.stepper.addEventListener('stepClick', (event) => {
  const index = event.detail?.index ?? 0;
  setSteps(index);
  const cards = ['step-auth', 'step-project', 'step-folder', 'step-local'];
  const target = document.getElementById(cards[Math.min(index, cards.length - 1)]);
  target?.scrollIntoView({ behavior: 'smooth', block: 'start' });
});

els.direction.options = [
  { label: 'Twee richtingen', value: 'TwoWay' },
  { label: 'Lokaal naar cloud', value: 'LocalToCloud' },
  { label: 'Cloud naar lokaal', value: 'CloudToLocal' },
];
els.direction.value = 'TwoWay';
els.slider.value = 60;
setSteps(0);

const params = new URLSearchParams(window.location.search);
if (params.get('loggedIn') === '1') {
  showAlert('success', 'Authenticatie geslaagd.');
  history.replaceState({}, '', '/');
}
if (params.get('authError')) {
  showAlert('error', `Inloggen mislukt: ${params.get('authError')}`);
}

try {
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
