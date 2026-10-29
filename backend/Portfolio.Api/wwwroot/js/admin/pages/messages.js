// Contact inbox: search/filter, read/unread and replied status, reply via email client, delete.
import { h, icon } from '../../lib/dom.js';
import { api } from '../../lib/api.js';
import { toast } from '../../lib/toast.js';
import { dateTime } from '../../lib/format.js';
import { confirmDialog, pageHeader, emptyState, loading, errorState } from '../ui.js';

const FILTERS = [['', 'All messages'], ['unread', 'Unread'], ['read', 'Read'], ['unreplied', 'Not replied'], ['replied', 'Replied'], ['failed', 'Email delivery failed']];
const DELIVERY = {
  Sent: ['text-bg-success', 'Email sent'], Failed: ['text-bg-danger', 'Email failed'], Pending: ['text-bg-secondary', 'Sending'],
  NotConfigured: ['text-bg-warning', 'Email not configured'], Disabled: ['text-bg-light border', 'Notifications off'],
};

export function messagesPage(ctx) {
  const state = { search: '', status: new URLSearchParams(location.search).get('status') ?? '', page: 1, selectedId: Number(new URLSearchParams(location.search).get('id')) || null, items: [], total: 0 };
  const listEl = h('div', { class: 'panel' }, loading());
  const detailEl = h('div', { class: 'panel' }, emptyState('envelope-open', 'Select a message to read it.'));
  const pagerEl = h('div', { class: 'd-flex justify-content-between align-items-center mt-3 small' });

  async function load() {
    const params = new URLSearchParams({ page: state.page, pageSize: 20 });
    if (state.search) params.set('search', state.search);
    if (state.status) params.set('status', state.status);
    try {
      const page = await api(`/api/admin/messages?${params}`);
      state.items = page.items; state.total = page.total;
      renderList();
      if (state.selectedId) select(state.selectedId, { silent: true });
    } catch (err) { listEl.replaceChildren(errorState(err.message, load)); }
  }

  function renderList() {
    if (!state.items.length) {
      listEl.replaceChildren(emptyState('inbox', state.search || state.status ? 'No messages match your filters.' : 'No messages yet. Contact form submissions appear here.'));
    } else {
      listEl.replaceChildren(h('div', { class: 'list-group list-group-flush message-list rounded-4', role: 'list' },
        state.items.map(m => h('button', {
          type: 'button', role: 'listitem',
          class: `list-group-item list-group-item-action py-3 ${m.isRead ? '' : 'unread'} ${m.id === state.selectedId ? 'active' : ''}`,
          'aria-current': m.id === state.selectedId ? 'true' : null,
          onclick: () => select(m.id),
        },
          h('div', { class: 'd-flex justify-content-between gap-2' },
            h('span', { class: 'fw-semibold text-truncate' }, !m.isRead && h('span', { class: 'badge text-bg-primary me-2' }, 'New'), m.name),
            h('small', { class: 'text-body-secondary text-nowrap' }, dateTime(m.createdAt))),
          h('div', { class: 'msg-subject text-truncate' }, m.subject),
          h('div', { class: 'small text-body-secondary text-truncate' }, m.message,
            m.isReplied && h('span', { class: 'visually-hidden' }, ' (replied)')),
          m.isReplied && h('span', { class: 'badge text-bg-success mt-1' }, icon('reply'), ' Replied')))));
    }
    const pages = Math.max(1, Math.ceil(state.total / 20));
    pagerEl.replaceChildren(
      h('span', { class: 'text-body-secondary' }, `${state.total} message${state.total === 1 ? '' : 's'}`),
      h('div', { class: 'btn-group btn-group-sm' },
        h('button', { type: 'button', class: 'btn btn-light', disabled: state.page <= 1, onclick: () => { state.page--; load(); } }, icon('chevron-left'), ' Prev'),
        h('span', { class: 'btn btn-light disabled' }, `${state.page} / ${pages}`),
        h('button', { type: 'button', class: 'btn btn-light', disabled: state.page >= pages, onclick: () => { state.page++; load(); } }, 'Next ', icon('chevron-right'))));
  }

  async function update(id, patch, message) {
    try {
      const updated = await api(`/api/admin/messages/${id}`, { method: 'PATCH', body: patch });
      const idx = state.items.findIndex(m => m.id === id);
      if (idx >= 0) state.items[idx] = updated;
      renderList(); renderDetail(updated); ctx.refreshBadges();
      if (message) toast(message);
    } catch (err) { toast(err.message, 'danger'); }
  }

  async function select(id, { silent = false } = {}) {
    state.selectedId = id;
    history.replaceState({}, '', `/admin/messages?id=${id}${state.status ? '&status=' + state.status : ''}`);
    try {
      const m = await api(`/api/admin/messages/${id}`);
      renderDetail(m);
      renderList();
      if (!m.isRead) await update(id, { isRead: true });
      if (!silent && window.innerWidth < 992) detailEl.scrollIntoView({ behavior: 'smooth' });
    } catch (err) {
      detailEl.replaceChildren(errorState(err.status === 404 ? 'This message no longer exists.' : err.message));
    }
  }

  function renderDetail(m) {
    const [badgeClass, badgeText] = DELIVERY[m.notificationStatus] ?? DELIVERY.Pending;
    const replyHref = `mailto:${encodeURIComponent(m.email)}?subject=${encodeURIComponent('Re: ' + m.subject)}`;
    detailEl.replaceChildren(h('article', { class: 'panel-body', 'aria-labelledby': 'msg-subject' },
      h('div', { class: 'd-flex flex-wrap justify-content-between gap-2 mb-3' },
        h('h2', { class: 'h5 mb-0', id: 'msg-subject' }, m.subject),
        h('div', { class: 'd-flex gap-1 flex-wrap' },
          h('span', { class: `badge ${badgeClass}` }, badgeText),
          m.isReplied && h('span', { class: 'badge text-bg-success' }, `Replied ${dateTime(m.repliedAt)}`))),
      h('dl', { class: 'row small mb-3' },
        h('dt', { class: 'col-sm-3 text-body-secondary fw-normal' }, 'From'), h('dd', { class: 'col-sm-9' }, m.name),
        h('dt', { class: 'col-sm-3 text-body-secondary fw-normal' }, 'Email'), h('dd', { class: 'col-sm-9' }, h('a', { href: `mailto:${m.email}` }, m.email)),
        m.phone && [h('dt', { class: 'col-sm-3 text-body-secondary fw-normal' }, 'Phone'), h('dd', { class: 'col-sm-9' }, h('a', { href: `tel:${m.phone.replace(/[^+\d]/g, '')}` }, m.phone))],
        h('dt', { class: 'col-sm-3 text-body-secondary fw-normal' }, 'Received'), h('dd', { class: 'col-sm-9' }, dateTime(m.createdAt))),
      h('div', { class: 'message-body pre-wrap mb-4' }, m.message),
      h('div', { class: 'd-flex flex-wrap gap-2' },
        h('a', { class: 'btn btn-primary', href: replyHref }, icon('reply'), ' Reply by email'),
        m.isReplied
          ? h('button', { type: 'button', class: 'btn btn-outline-primary', onclick: () => update(m.id, { isReplied: false }, 'Marked as not replied.') }, 'Mark not replied')
          : h('button', { type: 'button', class: 'btn btn-outline-primary', onclick: () => update(m.id, { isReplied: true }, 'Marked as replied.') }, icon('check2'), ' Mark replied'),
        h('button', { type: 'button', class: 'btn btn-light', onclick: () => update(m.id, { isRead: false }, 'Marked as unread.') }, icon('envelope'), ' Mark unread'),
        h('button', { type: 'button', class: 'btn btn-light text-danger ms-auto', onclick: async () => {
          if (!await confirmDialog({ title: 'Delete message?', message: `The message from ${m.name} will be permanently deleted.` })) return;
          try {
            await api(`/api/admin/messages/${m.id}`, { method: 'DELETE' });
            toast('Message deleted.');
            state.selectedId = null;
            history.replaceState({}, '', '/admin/messages');
            detailEl.replaceChildren(emptyState('envelope-open', 'Select a message to read it.'));
            ctx.refreshBadges(); load();
          } catch (err) { toast(err.message, 'danger'); }
        } }, icon('trash'), ' Delete')),
      h('p', { class: 'small text-body-secondary mt-3 mb-0' }, '"Reply by email" opens your email app; mark the message as replied once you have sent your answer.')));
  }

  let timer;
  const search = h('input', { type: 'search', class: 'form-control', placeholder: 'Search name, email, subject or message', 'aria-label': 'Search messages',
    oninput: e => { clearTimeout(timer); timer = setTimeout(() => { state.search = e.target.value.trim(); state.page = 1; load(); }, 300); } });
  const filter = h('select', { class: 'form-select', 'aria-label': 'Filter messages', onchange: e => { state.status = e.target.value; state.page = 1; load(); } },
    FILTERS.map(([value, label]) => h('option', { value }, label)));
  filter.value = state.status;

  load();
  return h('div', null,
    pageHeader('Messages', 'Inquiries sent through your portfolio contact form.'),
    h('div', { class: 'row g-2 mb-3' },
      h('div', { class: 'col-md-8' }, h('div', { class: 'input-group' }, h('span', { class: 'input-group-text' }, icon('search')), search)),
      h('div', { class: 'col-md-4' }, filter)),
    h('div', { class: 'row g-4' },
      h('div', { class: 'col-lg-5' }, listEl, pagerEl),
      h('div', { class: 'col-lg-7' }, detailEl)));
}
