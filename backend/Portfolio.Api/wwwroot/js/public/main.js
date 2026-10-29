// Public portfolio entry point: loads data once, renders sections, and handles /projects/:slug client-side.
import { h, icon, initials, qs, qsa } from '../lib/dom.js';
import { api } from '../lib/api.js';
import { renderHero, renderAbout, renderSkills, renderExperience, renderProjects, renderEducation, renderServices, renderFooter } from './sections.js';
import { renderContact } from './contact.js';
import { renderProjectDetail } from '../shared/project-view.js';

const views = { status: qs('#app-status'), home: qs('#home'), detail: qs('#project-detail'), notFound: qs('#not-found') };
let data = null;
const defaultTitle = document.title;

function show(name) {
  for (const [key, el] of Object.entries(views)) el.hidden = key !== name;
}

function setMeta(title, description) {
  document.title = title;
  qs('meta[name="description"]')?.setAttribute('content', description);
}

function revealOnScroll(root) {
  const items = qsa('.reveal', root);
  if (!('IntersectionObserver' in window) || window.matchMedia('(prefers-reduced-motion: reduce)').matches) {
    items.forEach(el => el.classList.add('is-visible'));
    return;
  }
  const observer = new IntersectionObserver(entries => entries.forEach(entry => {
    if (entry.isIntersecting) { entry.target.classList.add('is-visible'); observer.unobserve(entry.target); }
  }), { rootMargin: '0px 0px -8% 0px' });
  items.forEach(el => observer.observe(el));
}

function openProject(e, slug) {
  if (e && (e.metaKey || e.ctrlKey || e.shiftKey || e.button === 1)) return;
  e?.preventDefault();
  history.pushState({}, '', `/projects/${slug}`);
  route();
  window.scrollTo({ top: 0 });
}

/** Render every section from data; sections without content are hidden along with their nav link. */
function renderHome() {
  const sections = {
    hero: renderHero(data),
    about: renderAbout(data),
    skills: data.skills.length ? renderSkills(data) : null,
    experience: data.experience.length ? renderExperience(data) : null,
    projects: data.projects.length ? renderProjects(data, { onOpen: openProject }) : null,
    education: data.education.length || data.achievements.length ? renderEducation(data) : null,
    services: data.services.length ? renderServices(data) : null,
    contact: renderContact(data),
  };
  for (const [id, content] of Object.entries(sections)) {
    const section = qs(`#${id}`);
    section.hidden = !content;
    section.replaceChildren(...(content ? [content] : []));
    qsa(`#nav-list [data-section-link="${id}"]`).forEach(a => { a.closest('li').hidden = !content; });
  }
  qs('#site-footer').replaceChildren(renderFooter(data));
  revealOnScroll(views.home);
}

function renderNotFound() {
  views.notFound.replaceChildren(h('div', { class: 'container text-center py-5' },
    h('p', { class: 'display-1 fw-bold text-brand mb-2' }, '404'),
    h('h1', { class: 'h3' }, 'Page not found'),
    h('p', { class: 'text-body-secondary mb-4' }, "The page you're looking for doesn't exist or was moved."),
    h('a', { class: 'btn btn-primary', href: '/', 'data-route': '' }, icon('house'), ' Back to home')));
  show('notFound');
}

function route() {
  const path = location.pathname.replace(/\/+$/, '') || '/';
  const match = path.match(/^\/projects\/([a-z0-9-]+)$/);
  const name = data.profile.fullName;

  if (path === '/') {
    show('home');
    setMeta(data.profile.seoTitle || defaultTitle, data.profile.seoDescription || data.profile.summary || '');
    if (location.hash) requestAnimationFrame(() => qs(location.hash)?.scrollIntoView());
    return;
  }
  if (match) {
    const project = data.projects.find(p => p.slug === match[1]);
    if (project) {
      views.detail.replaceChildren(renderProjectDetail(project, {
        onBack: e => { e.preventDefault(); history.pushState({}, '', '/'); route(); qs('#projects').scrollIntoView(); },
      }));
      show('detail');
      setMeta(`${project.title} — ${name}`, project.summary);
      views.detail.focus?.();
      return;
    }
  }
  setMeta(`Page not found — ${name}`, '');
  renderNotFound();
}

// Same-page navigation for section links and in-app routes without full reloads.
document.addEventListener('click', e => {
  const link = e.target.closest('a[data-section-link], a[data-route]');
  if (!link || e.metaKey || e.ctrlKey || e.shiftKey || !data) return;
  e.preventDefault();
  const id = link.getAttribute('data-section-link');
  const target = id ? `/#${id}` : link.getAttribute('href');
  if (location.pathname !== '/' || !id) {
    history.pushState({}, '', target);
    route();
  } else {
    history.replaceState({}, '', target);
  }
  if (id) qs(`#${id}`)?.scrollIntoView();
  else window.scrollTo({ top: 0 });
  const collapse = window.bootstrap?.Collapse.getInstance(qs('#nav-links'));
  collapse?.hide();
});
window.addEventListener('popstate', () => data && route());

// Highlight the nav link for the section in view.
function trackActiveSection() {
  if (!('IntersectionObserver' in window)) return;
  const links = qsa('#nav-list .nav-link');
  const observer = new IntersectionObserver(entries => entries.forEach(entry => {
    if (!entry.isIntersecting) return;
    links.forEach(a => {
      const active = a.dataset.sectionLink === entry.target.id;
      a.classList.toggle('active', active);
      if (active) a.setAttribute('aria-current', 'true'); else a.removeAttribute('aria-current');
    });
  }), { rootMargin: '-45% 0px -50% 0px' });
  qsa('#home > section[id]').forEach(s => observer.observe(s));
}

function addStructuredData() {
  const p = data.profile;
  const script = document.createElement('script');
  script.type = 'application/ld+json';
  script.textContent = JSON.stringify({
    '@context': 'https://schema.org', '@type': 'Person', name: p.fullName, jobTitle: p.headline,
    url: location.origin, email: p.email ? `mailto:${p.email}` : undefined, image: p.imageUrl ? location.origin + p.imageUrl : undefined,
    sameAs: [p.gitHubUrl, p.linkedInUrl, p.websiteUrl].filter(Boolean),
  });
  document.head.append(script);
}

async function init() {
  try {
    data = await api('/api/public/portfolio');
  } catch (err) {
    views.status.replaceChildren(
      h('i', { class: 'bi bi-cloud-slash fs-1 text-body-secondary', 'aria-hidden': 'true' }),
      h('p', { class: 'mt-3' }, 'The portfolio could not be loaded. ', err.message),
      h('button', { class: 'btn btn-primary', type: 'button', onclick: () => location.reload() }, 'Try again'));
    return;
  }
  qs('#brand-name').textContent = data.profile.fullName;
  qs('#brand-mark').textContent = initials(data.profile.fullName);
  renderHome();
  trackActiveSection();
  addStructuredData();
  route();
}

init();
