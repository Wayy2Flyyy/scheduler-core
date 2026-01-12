const jobsTable = document.getElementById('jobsTable');
const statusFilter = document.getElementById('statusFilter');
const refreshBtn = document.getElementById('refreshBtn');
const details = document.getElementById('jobDetailsBody');

async function fetchJobs() {
  const status = statusFilter.value;
  const params = new URLSearchParams();
  if (status) {
    params.set('status', status);
  }
  params.set('take', '50');
  const response = await fetch(`/api/jobs?${params.toString()}`);
  const data = await response.json();
  renderJobs(data.jobs || []);
}

function renderJobs(jobs) {
  jobsTable.innerHTML = '';
  jobs.forEach(job => {
    const row = document.createElement('tr');
    row.innerHTML = `
      <td class="mono">${job.id}</td>
      <td>${job.type}</td>
      <td><span class="status status-${job.status.toLowerCase()}">${job.status}</span></td>
      <td>${new Date(job.runAt).toLocaleString()}</td>
      <td>${job.attempts} / ${job.maxAttempts}</td>
      <td>
        <button data-action="view" data-id="${job.id}">View</button>
        <button data-action="retry" data-id="${job.id}">Retry</button>
        <button data-action="cancel" data-id="${job.id}">Cancel</button>
      </td>`;
    jobsTable.appendChild(row);
  });
}

jobsTable.addEventListener('click', async (event) => {
  const button = event.target.closest('button');
  if (!button) return;

  const jobId = button.dataset.id;
  const action = button.dataset.action;

  if (action === 'view') {
    const response = await fetch(`/api/jobs/${jobId}`);
    const job = await response.json();
    details.textContent = JSON.stringify(job, null, 2);
  }

  if (action === 'retry') {
    await fetch(`/api/jobs/${jobId}/retry`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ reason: 'Manual retry' }) });
    await fetchJobs();
  }

  if (action === 'cancel') {
    await fetch(`/api/jobs/${jobId}/cancel`, { method: 'POST' });
    await fetchJobs();
  }
});

refreshBtn.addEventListener('click', fetchJobs);
statusFilter.addEventListener('change', fetchJobs);

fetchJobs();
