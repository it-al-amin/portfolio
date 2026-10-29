// Admin entry point: session check, sidebar layout and client-side routing for /admin/*.
import { h, icon, qs } from '../lib/dom.js';
import { api, onUnauthorized } from '../lib/api.js';
import { loading, errorState } from './ui.js';
import { loginPage } from './pages/login.js';
import { dashboardPage } from './pages/dashboard.js';
import { profilePage, settingsPage } from './pages/profile.js';
import { experiencePage, educationPage, servicesPage, certificationsPage } from './pages/content.js';
import { skillsPage } from './pages/skills.js';
import { projectsListPage, projectEditorPage, projectPreviewPage } from './pages/projects.js';
import { documentsPage } from './pages/documents.js';
import { messagesPage } from './pages/messages.js';
import { emailSettingsPage } from './pages/email-settings.js';

const root = qs('#admin-root');
let session = null;
let renderId = 0;

const nav = [
  { heading: 'Overview' },
  { href: '/admin', label: 'Dashboard', icon: 'speedometer2' },
  { href: '/admin/messages', label: 'Messages', icon: 'inbox', badge: 'unread' },
  { heading: 'Content' },
  { href: '/admin/profile', label: 'Profile', icon: 'person-badge' },
  { href: '/admin/experience', label: 'Experience', icon: 'briefcase' },
  { href: '/admin/skills', label: 'Skills', icon: 'tools' },
  { href: '/admin/projects', label: 'Projects', icon: 'kanban' },
  { href: '/admin/education', label: 'Education', icon: 'mortarboard' },
  { href: '/admin/certifications', label: 'Achievements', icon: 'trophy' },
  { href: '/admin/services', label: 'Services', icon: 'grid' },
  { href: '/admin/documents', label: 'Documents & CV', icon: 'folder2' },
  { heading: 'Settings' },
  { href: '/admin/settings/email', label: 'Email', icon: 'envelope-at' },
  { href: '/admin/settings', label: 'SEO & account', icon: 'gear' },
];

/** Ordered route table; the first pattern that matches the path wins. */
const routes = [
  [/^\/admin\/?$/, () => dashboardPage(ctx)],
  [/^\/admin\/messages$/, () => messagesPage(ctx)],
  [/^\/admin\/profile$/, () => profilePage(ctx)],
  [/^\/admin\/experience$/, () => experiencePage(ctx)],
  [/^\/admin\/skills$/, () => skillsPage(ctx)],
  [/^\/admin\/projects$/, () => projectsListPage(ctx)],
  [/^\/admin\/projects\/new$/, () => projectEditorPage(ctx, null)],
  [/^\/admin\/projects\/([^/]+)\/preview$/, m => projectPreviewPage(ctx, m[1])],
  [/^\/admin\/projects\/([^/]+)$/, m => projectEditorPage(ctx, m[1])],
  [/^\/admin\/education$/, () => educationPage(ctx)],
  [/^\/admin\/certifications$/, () => certificationsPage(ctx)],
  [/^\/admin\/services$/, () => servicesPage(ctx)],
  [/^\/admin\/documents$/, () => documentsPage(ctx)],
  [/^\/admin\/settings\/email$/, () => emailSettingsPage(ctx)],
  [/^\/admin\/settings$/, () => settingsPage(ctx)],
];

const ctx = {
  navigate(path, { replace = false } = {}) {
    if (replace) history.replaceState({}, '', path); else history.pushState({}, '', path);
    render();
  },
  async refreshBadges() {
    try {
      const d = await api('/api/admin/dashboard');
      document.querySelectorAll('[data-badge="unread"]').forEach(b => { b.textContent = d.messages.unread; b.hidden = d.messages.unread === 0; });
    } catch { /* badges are cosmetic */ }
  },
};

function isActive(href, path) {
  if (href === '/admin') return path === '/admin' || path === '/admin/';
  if (href === '/admin/settings') return path === href;
  return path === href || path.startsWith(href + '/');
}

function sidebar(path) {
  return h('aside', { class: 'admin-sidebar offcanvas-lg offcanvas-start', id: 'admin-sidebar', tabindex: '-1', 'aria-label': 'Admin navigation' },
    h('div', { class: 'offcanvas-header border-bottom' },
      h('span', { class: 'fw-semibold' }, 'Menu'),
      h('button', { type: 'button', class: 'btn-close', 'data-bs-dismiss': 'offcanvas', 'data-bs-target': '#admin-sidebar', 'aria-label': 'Close menu' })),
    h('div', { class: 'offcanvas-body' },
      h('a', { href: '/admin', class: 'd-flex align-items-center gap-2 text-decoration-none px-2 pb-2' },
        h('span', { class: 'brand-mark', 'aria-hidden': 'true' }, icon('shield-lock')),
        h('span', { class: 'fw-semibold text-body-emphasis' }, 'Portfolio Admin')),
      h('nav', { class: 'admin-nav nav flex-column' }, nav.map(item => item.heading
        ? h('div', { class: 'nav-heading' }, item.heading)
        : h('a', { class: `nav-link${isActive(item.href, path) ? ' active' : ''}`, href: item.href, 'aria-current': isActive(item.href, path) ? 'page' : null },
            icon(item.icon), h('span', { class: 'flex-grow-1' }, item.label),
            item.badge && h('span', { class: 'badge rounded-pill text-bg-primary', 'data-badge': item.badge, hidden: true })))),
      h('div', { class: 'mt-auto pt-3 border-top small text-body-secondary px-2 text-truncate' }, icon('person-circle'), ' ', session.email)));
}

function topbar() {
  const logout = h('button', { type: 'button', class: 'btn btn-sm btn-outline-secondary' }, icon('box-arrow-right'), h('span', { class: 'd-none d-sm-inline' }, ' Sign out'));
  logout.addEventListener('click', async () => {
    try { await api('/api/auth/logout', { method: 'POST', skipAuthRedirect: true }); } catch { /* signing out locally regardless */ }
    session = null;
    ctx.navigate('/admin/login');
  });
  return h('header', { class: 'admin-topbar' },
    h('button', { type: 'button', class: 'btn btn-icon d-lg-none', 'data-bs-toggle': 'offcanvas', 'data-bs-target': '#admin-sidebar', 'aria-controls': 'admin-sidebar', 'aria-label': 'Open menu' }, icon('list', 'fs-4')),
    h('div', { class: 'flex-grow-1' }),
    h('a', { class: 'btn btn-sm btn-light', href: '/', target: '_blank', rel: 'noopener' }, icon('box-arrow-up-right'), h('span', { class: 'd-none d-sm-inline' }, ' View site')),
    h('button', { type: 'button', class: 'btn btn-icon', 'data-theme-toggle': '', 'aria-label': 'Toggle dark mode' }, icon('moon-stars')),
    logout);
}

function showLogin() {
  const next = location.pathname.startsWith('/admin/login') ? '/admin' : location.pathname + location.search;
  if (!location.pathname.startsWith('/admin/login')) history.replaceState({}, '', '/admin/login');
  root.replaceChildren(loginPage({ onSuccess: s => { session = s; ctx.navigate(next, { replace: true }); } }));
}

async function render() {
  const id = ++renderId;
  const path = location.pathname.replace(/\/+$/, '') || '/admin';
  if (!session) {
    try { session = await api('/api/auth/me', { skipAuthRedirect: true }); } catch { session = null; }
    if (id !== renderId) return;
  }
  if (!session) return showLogin();
  if (path === '/admin/login') return ctx.navigate('/admin', { replace: true });

  const content = h('main', { class: 'admin-content', id: 'admin-main', tabindex: '-1' }, loading());
  root.replaceChildren(h('div', { class: 'admin-shell' }, sidebar(path), h('div', { class: 'admin-main' }, topbar(), content)));
  window.bootstrap.Offcanvas.getInstance('#admin-sidebar')?.hide();
  ctx.refreshBadges();

  const match = routes.map(([re, page]) => [path.match(re), page]).find(([m]) => m);
  try {
    const view = match ? await match[1](match[0]) : errorState('This admin page does not exist.');
    if (id !== renderId) return;
    content.replaceChildren(view);
    const title = qs('h1', content)?.textContent;
    document.title = title ? `${title} · Admin` : 'Admin · Portfolio';
    content.focus({ preventScroll: true });
    window.scrollTo(0, 0);
  } catch (err) {
    if (id !== renderId || err?.status === 401) return;
    content.replaceChildren(errorState(err.message || 'Could not load this page.', render));
  }
}

onUnauthorized(() => { session = null; showLogin(); });

// Same-tab links inside /admin are handled client-side; downloads, new tabs and API links go to the browser.
document.addEventListener('click', e => {
  const link = e.target.closest('a[href]');
  if (!link || e.defaultPrevented || e.button !== 0 || e.metaKey || e.ctrlKey || e.shiftKey || e.altKey) return;
  if (link.target || link.hasAttribute('download')) return;
  const url = new URL(link.href, location.href);
  if (url.origin !== location.origin || !url.pathname.startsWith('/admin')) return;
  e.preventDefault();
  if (url.pathname + url.search !== location.pathname + location.search) ctx.navigate(url.pathname + url.search);
});
window.addEventListener('popstate', render);

render();
