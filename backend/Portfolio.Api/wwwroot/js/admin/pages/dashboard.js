// Dashboard: content statistics, setup warnings and recent messages.
import { h, icon } from '../../lib/dom.js';
import { api } from '../../lib/api.js';
import { dateTime } from '../../lib/format.js';
import { pageHeader, alertBox, emptyState } from '../ui.js';

const stat = (label, value, sub, iconName, href) => h('div', { class: 'col-6 col-md-4 col-xl-3' },
  h('a', { class: 'stat-card d-block text-decoration-none', href },
    h('div', { class: 'd-flex justify-content-between align-items-start mb-2' },
      h('span', { class: 'stat-label' }, label), h('span', { class: 'icon-tile' }, icon(iconName))),
    h('div', { class: 'stat-value' }, value),
    sub && h('div', { class: 'small text-body-secondary mt-1' }, sub)));

export async function dashboardPage() {
  const d = await api('/api/admin/dashboard');
  const warnings = [
    !d.emailConfigured && h('span', null, 'Email delivery is not configured — contact messages are saved to the inbox but no notification emails are sent. ', h('a', { href: '/admin/settings/email' }, 'Configure email')),
    d.emailConfigured && !d.emailPasswordConfigured && h('span', null, 'An SMTP host is set but no SMTP password is configured on the server. Most providers require one. ', h('a', { href: '/admin/settings/email' }, 'Review email settings')),
    !d.hasPublicCv && h('span', null, 'No public CV is uploaded, so the "Download CV" buttons are hidden. ', h('a', { href: '/admin/documents' }, 'Upload your CV')),
    d.messages.failedNotifications > 0 && h('span', null, `${d.messages.failedNotifications} message notification${d.messages.failedNotifications > 1 ? 's' : ''} failed to send. `, h('a', { href: '/admin/messages?status=failed' }, 'View messages')),
  ].filter(Boolean);

  return h('div', null,
    pageHeader('Dashboard', 'An overview of your portfolio content.',
      h('a', { class: 'btn btn-outline-primary', href: '/', target: '_blank', rel: 'noopener' }, icon('box-arrow-up-right'), ' View site')),
    warnings.map(w => alertBox(w, 'warning')),
    h('div', { class: 'row g-3 mb-4' },
      stat('Projects', d.projects.total, `${d.projects.published} published · ${d.projects.featured} featured`, 'kanban', '/admin/projects'),
      stat('Unread messages', d.messages.unread, `${d.messages.total} total`, 'inbox', '/admin/messages?status=unread'),
      stat('Skills', d.skills, null, 'tools', '/admin/skills'),
      stat('Experience', d.experiences, null, 'briefcase', '/admin/experience'),
      stat('Education', d.education, null, 'mortarboard', '/admin/education'),
      stat('Achievements', d.achievements, null, 'trophy', '/admin/certifications'),
      stat('Services', d.services, null, 'grid', '/admin/services'),
      stat('Documents', d.documents.total, `${d.documents.public} public`, 'folder2', '/admin/documents')),
    h('section', { class: 'panel' },
      h('div', { class: 'panel-header d-flex justify-content-between align-items-center' },
        h('h2', { class: 'h6 mb-0' }, 'Recent messages'), h('a', { href: '/admin/messages', class: 'small' }, 'View all')),
      d.recentMessages.length
        ? h('div', { class: 'list-group list-group-flush rounded-bottom-4' }, d.recentMessages.map(m =>
            h('a', { class: 'list-group-item list-group-item-action d-flex justify-content-between gap-3', href: `/admin/messages?id=${m.id}` },
              h('span', { class: 'text-truncate' }, !m.isRead && h('span', { class: 'badge text-bg-primary me-2' }, 'New'), h('strong', null, m.name), ' — ', m.subject),
              h('small', { class: 'text-body-secondary text-nowrap' }, dateTime(m.createdAt)))))
        : emptyState('inbox', 'No messages yet.')));
}
