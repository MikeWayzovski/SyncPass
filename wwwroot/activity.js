import { defineCustomElements } from 'https://cdn.jsdelivr.net/npm/@trimble-oss/moduswebcomponents@1.17.0/loader/index.js';

await defineCustomElements(undefined, {
  resourcesUrl: 'https://cdn.jsdelivr.net/npm/@trimble-oss/moduswebcomponents@1.17.0/dist/',
});

const ACTION_COLOR = {
  UPLOAD: 'success',
  DOWNLOAD: 'primary',
  DELETE: 'warning',
  CONFLICT: 'secondary',
  ERROR: 'danger',
};

const table = document.getElementById('activity-table');
const emptyState = document.getElementById('empty-state');
const refreshBadge = document.getElementById('refresh-badge');
const alertEl = document.getElementById('page-alert');

function actionBadge(value) {
  const action = String(value || '').toUpperCase();
  const badge = document.createElement('modus-wc-badge');
  badge.size = 'sm';
  badge.variant = 'filled';
  badge.color = ACTION_COLOR[action] || 'default';
  badge.textContent = action || '—';
  return badge;
}

table.columns = [
  { id: 'timestamp', header: 'Tijdstip', accessor: 'timestamp', width: '180px' },
  { id: 'action', header: 'Actie', accessor: 'action', width: '130px', cellRenderer: (value) => actionBadge(value) },
  { id: 'filePath', header: 'Bestand', accessor: 'filePath' },
  { id: 'details', header: 'Details', accessor: 'details' },
];
table.sortable = false;
table.zebra = true;

function formatTimestamp(value) {
  if (!value) {
    return '—';
  }
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? String(value) : date.toLocaleString();
}

function showAlert(message) {
  if (!message) {
    alertEl.hidden = true;
    return;
  }
  alertEl.hidden = false;
  alertEl.variant = 'error';
  alertEl.alertDescription = message;
}

async function refresh() {
  try {
    const response = await fetch('/api/activity', { headers: { Accept: 'application/json' } });
    if (!response.ok) {
      throw new Error(`HTTP ${response.status}`);
    }
    const rows = await response.json();
    const data = (Array.isArray(rows) ? rows : []).map((row) => ({
      timestamp: formatTimestamp(row.timestampUtc),
      action: row.action || '',
      filePath: row.filePath || '—',
      details: row.details || '—',
    }));
    table.data = data;
    emptyState.hidden = data.length > 0;
    refreshBadge.textContent = `Bijgewerkt ${new Date().toLocaleTimeString()}`;
    showAlert(null);
  } catch (error) {
    refreshBadge.textContent = 'Vernieuwen mislukt';
    showAlert(error.message);
  }
}

document.getElementById('setup-btn').addEventListener('buttonClick', () => {
  window.location.href = '/';
});
document.getElementById('setup-btn').addEventListener('click', () => {
  window.location.href = '/';
});

await refresh();
window.setInterval(refresh, 4000);
