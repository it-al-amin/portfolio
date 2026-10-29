// Renderers for each public portfolio section. Each returns nodes built from API data (no hardcoded content).
import { h, icon, externalLink, initials } from '../lib/dom.js';
import { monthYear, duration, paragraphs, date } from '../lib/format.js';
import { projectThumb, projectLinks } from '../shared/project-view.js';

const sectionHeader = (id, eyebrow, title, lead) => h('div', { class: 'mb-5 reveal' },
  h('p', { class: 'section-eyebrow' }, eyebrow),
  h('h2', { class: 'section-title', id: `${id}-title` }, title),
  lead && h('p', { class: 'section-lead mb-0' }, lead));

export function socialLinks(profile, extraClass = '') {
  const links = [
    profile.gitHubUrl && { href: profile.gitHubUrl, label: 'GitHub', icon: 'github' },
    profile.linkedInUrl && { href: profile.linkedInUrl, label: 'LinkedIn', icon: 'linkedin' },
    profile.websiteUrl && { href: profile.websiteUrl, label: 'Website', icon: 'globe2' },
    profile.email && { href: `mailto:${profile.email}`, label: 'Email', icon: 'envelope', internal: true },
  ].filter(Boolean);
  if (!links.length) return null;
  return h('ul', { class: `social-links list-unstyled d-flex gap-2 mb-0 ${extraClass}` },
    links.map(l => h('li', null, l.internal
      ? h('a', { href: l.href, 'aria-label': l.label, title: l.label }, icon(l.icon))
      : externalLink(l.href, icon(l.icon), { 'aria-label': `${l.label} (opens in a new tab)`, title: l.label }))));
}

export function renderHero(data) {
  const p = data.profile;
  const highlights = (p.highlights || []).map(item => {
    const [value, ...label] = item.split('|');
    return { value: value.trim(), label: label.join('|').trim() };
  }).filter(x => x.value);

  const portrait = p.imageUrl
    ? h('img', { class: 'portrait', src: p.imageUrl, alt: `Portrait of ${p.fullName}`, width: 360, height: 360, fetchpriority: 'high' })
    : h('div', { class: 'portrait-fallback', role: 'img', 'aria-label': p.fullName }, initials(p.fullName));

  return h('div', { class: 'container' },
    h('div', { class: 'row align-items-center g-5' },
      h('div', { class: 'col-lg-7' },
        p.availabilityStatus && h('p', { class: 'status-pill mb-4' }, h('span', { class: 'status-dot', 'aria-hidden': 'true' }), p.availabilityStatus),
        h('h1', { class: 'hero-name' }, p.fullName),
        h('p', { class: 'hero-headline' }, p.headline),
        p.summary && h('p', { class: 'hero-summary mb-4' }, p.summary),
        p.location && h('p', { class: 'text-body-secondary d-flex align-items-center gap-2 mb-4' }, icon('geo-alt'), p.location),
        h('div', { class: 'd-flex flex-wrap gap-2 mb-4' },
          h('a', { class: 'btn btn-primary btn-lg', href: '/#projects', 'data-section-link': 'projects' }, 'View projects ', icon('arrow-right')),
          data.cvUrl && h('a', { class: 'btn btn-outline-primary btn-lg', href: data.cvUrl, download: '' }, icon('download'), ' Download CV'),
          h('a', { class: 'btn btn-link btn-lg text-decoration-none', href: '/#contact', 'data-section-link': 'contact' }, icon('chat-dots'), ' Contact me')),
        socialLinks(p)),
      h('div', { class: 'col-lg-5' },
        h('div', { class: 'portrait-wrap' }, portrait),
        highlights.length > 0 && h('ul', { class: 'row g-3 mt-3 list-unstyled', 'aria-label': 'Highlights' },
          highlights.map(x => h('li', { class: `col-${12 / Math.min(highlights.length, 3)}` },
            h('div', { class: 'highlight-card text-center' },
              h('div', { class: 'highlight-value' }, x.value),
              h('div', { class: 'highlight-label' }, x.label))))))));
}

export function renderAbout(data) {
  const p = data.profile;
  const edu = data.education[0];
  return h('div', { class: 'container' },
    sectionHeader('about', 'About me', 'Engineer, problem solver, collaborator'),
    h('div', { class: 'row g-5' },
      h('div', { class: 'col-lg-7 reveal' }, paragraphs(p.bio || p.summary).map(text => h('p', { class: 'fs-6 text-body-secondary lh-lg' }, text))),
      h('div', { class: 'col-lg-5 reveal' },
        p.careerInterests?.length > 0 && h('div', { class: 'card-soft p-4 mb-4' },
          h('h3', { class: 'h6 text-uppercase text-body-secondary small mb-3' }, 'Technical focus'),
          h('ul', { class: 'list-unstyled mb-0' }, p.careerInterests.map(item =>
            h('li', { class: 'd-flex gap-2 mb-2' }, icon('check2-circle', 'text-brand mt-1'), item)))),
        edu && h('div', { class: 'card-soft p-4 d-flex gap-3' },
          h('span', { class: 'icon-tile' }, icon('mortarboard')),
          h('div', null,
            h('h3', { class: 'h6 mb-1' }, [edu.degree, edu.fieldOfStudy].filter(Boolean).join(' in ')),
            h('p', { class: 'mb-1 text-body-secondary small' }, edu.institution),
            h('p', { class: 'mb-0 small fw-semibold text-brand' }, [edu.endYear, edu.grade].filter(Boolean).join(' · ')))))));
}

export function renderSkills(data) {
  return h('div', { class: 'container' },
    sectionHeader('skills', 'Technical skills', 'Tools I build with', 'Languages, frameworks and platforms I use in production work.'),
    h('div', { class: 'row g-4' }, data.skills.map(cat => h('div', { class: 'col-md-6 col-xl-4 reveal' },
      h('div', { class: 'card-soft card-hover p-4 h-100' },
        h('div', { class: 'd-flex align-items-center gap-3 mb-3' },
          h('span', { class: 'icon-tile' }, icon(cat.icon || 'grid')),
          h('h3', { class: 'h5 mb-0' }, cat.name)),
        h('ul', { class: 'chip-list' }, cat.skills.map(s => h('li', { class: 'tag' }, s.name,
          s.proficiency && h('span', { class: 'tag-proficiency' }, `· ${s.proficiency}`)))))))));
}

export function renderExperience(data) {
  return h('div', { class: 'container' },
    sectionHeader('experience', 'Work experience', 'Where I have worked'),
    h('ol', { class: 'timeline' }, data.experience.map(job => h('li', { class: `timeline-item reveal ${job.isCurrent ? 'is-current' : ''}` },
      h('span', { class: 'timeline-marker', 'aria-hidden': 'true' }, icon('briefcase')),
      h('div', { class: 'card-soft p-4' },
        h('div', { class: 'd-flex flex-wrap justify-content-between gap-2 mb-2' },
          h('div', null,
            h('h3', { class: 'h5 mb-1' }, job.designation),
            h('p', { class: 'mb-0 fw-semibold text-brand' }, job.company)),
          h('div', { class: 'text-md-end small text-body-secondary' },
            h('div', { class: 'fw-semibold text-body' },
              `${monthYear(job.startDate)} – ${job.isCurrent ? 'Present' : monthYear(job.endDate)}`,
              job.isCurrent && h('span', { class: 'badge text-bg-success ms-2' }, 'Current')),
            h('div', null, [duration(job.startDate, job.isCurrent ? null : job.endDate), job.employmentType, job.location].filter(Boolean).join(' · ')))),
        job.summary && h('p', { class: 'text-body-secondary' }, job.summary),
        job.responsibilities.length > 0 && h('ul', { class: 'resp-list mb-3' }, job.responsibilities.map(r => h('li', null, r))),
        job.technologies.length > 0 && h('ul', { class: 'chip-list', 'aria-label': 'Technologies' }, job.technologies.map(t => h('li', { class: 'tag' }, t))))))));
}

/** Projects with search + technology filters. State lives in the closure; only the grid re-renders. */
export function renderProjects(data, { onOpen }) {
  const projects = data.projects;
  const counts = new Map();
  projects.forEach(p => p.technologies.forEach(t => counts.set(t, (counts.get(t) || 0) + 1)));
  const topTech = [...counts.entries()].sort((a, b) => b[1] - a[1] || a[0].localeCompare(b[0])).slice(0, 8).map(([t]) => t);

  let activeTech = null;
  let term = '';
  const grid = h('div', { class: 'row g-4' });
  const status = h('p', { class: 'visually-hidden', 'aria-live': 'polite' });

  const card = p => h('div', { class: 'col-md-6 col-xl-4' },
    h('article', { class: 'card-soft card-hover project-card' },
      projectThumb(p),
      h('div', { class: 'p-4 d-flex flex-column flex-grow-1' },
        h('div', { class: 'd-flex flex-wrap gap-2 mb-2' },
          p.category && h('span', { class: 'badge rounded-pill bg-brand-soft text-brand' }, p.category),
          p.isFeatured && h('span', { class: 'badge rounded-pill featured-badge' }, icon('star-fill'), ' Featured')),
        h('h3', { class: 'h5 card-title' }, h('a', { href: `/projects/${p.slug}`, onclick: e => onOpen(e, p.slug) }, p.title)),
        h('p', { class: 'text-body-secondary small flex-grow-1' }, p.summary),
        h('ul', { class: 'chip-list mb-3', 'aria-label': 'Technologies' },
          p.technologies.slice(0, 5).map(t => h('li', { class: 'tag' }, t)),
          p.technologies.length > 5 && h('li', { class: 'tag' }, `+${p.technologies.length - 5}`)),
        h('div', { class: 'project-actions d-flex flex-wrap gap-2' },
          h('a', { class: 'btn btn-sm btn-outline-primary', href: `/projects/${p.slug}`, onclick: e => onOpen(e, p.slug) }, 'Details'),
          projectLinks(p, 'btn-sm').map(btn => { btn.classList.replace('btn-primary', 'btn-light'); return btn; })))));

  function update() {
    const q = term.trim().toLowerCase();
    const visible = projects.filter(p =>
      (!activeTech || p.technologies.includes(activeTech)) &&
      (!q || [p.title, p.summary, p.category, ...p.technologies].join(' ').toLowerCase().includes(q)));
    grid.replaceChildren(...(visible.length
      ? visible.map(card)
      : [h('div', { class: 'col-12' }, h('div', { class: 'card-soft p-5 text-center text-body-secondary' },
          icon('search', 'fs-2 d-block mb-2'), 'No projects match your filters.'))]));
    status.textContent = `${visible.length} project${visible.length === 1 ? '' : 's'} shown`;
  }

  const chip = (label, tech) => h('button', {
    type: 'button', class: 'filter-chip', 'aria-pressed': String(activeTech === tech),
    onclick: e => {
      activeTech = tech;
      e.currentTarget.parentElement.querySelectorAll('.filter-chip').forEach(b => b.setAttribute('aria-pressed', String(b === e.currentTarget)));
      update();
    },
  }, label);

  update();
  return h('div', { class: 'container' },
    sectionHeader('projects', 'Projects', 'Selected work', 'Production applications and platforms I have built or contributed to.'),
    h('div', { class: 'd-flex flex-column flex-lg-row gap-3 justify-content-between align-items-lg-center mb-4' },
      h('div', { class: 'filter-bar', role: 'group', 'aria-label': 'Filter projects by technology' }, chip('All', null), topTech.map(t => chip(t, t))),
      h('div', { class: 'input-group project-search flex-shrink-0' },
        h('span', { class: 'input-group-text bg-transparent' }, icon('search')),
        h('input', { type: 'search', class: 'form-control', placeholder: 'Search projects', 'aria-label': 'Search projects',
          oninput: e => { term = e.target.value; update(); } }))),
    status,
    grid);
}

export function renderEducation(data) {
  const achievements = data.achievements;
  return h('div', { class: 'container' },
    sectionHeader('education', 'Education & achievements', 'Learning and recognition'),
    h('div', { class: 'row g-4' },
      h('div', { class: 'col-lg-5' },
        h('h3', { class: 'h6 text-uppercase text-body-secondary small mb-3' }, 'Education'),
        data.education.map(e => h('div', { class: 'card-soft p-4 mb-3 reveal d-flex gap-3' },
          h('span', { class: 'icon-tile' }, icon('mortarboard')),
          h('div', null,
            h('h4', { class: 'h5 mb-1' }, e.degree),
            e.fieldOfStudy && h('p', { class: 'mb-1 fw-semibold text-brand' }, e.fieldOfStudy),
            h('p', { class: 'mb-2 text-body-secondary' }, e.institution),
            h('p', { class: 'mb-0 small' }, [e.startYear && e.endYear ? `${e.startYear} – ${e.endYear}` : e.endYear, e.grade].filter(Boolean).join(' · ')),
            e.description && h('p', { class: 'small text-body-secondary mt-2 mb-0' }, e.description))))),
      achievements.length > 0 && h('div', { class: 'col-lg-7' },
        h('h3', { class: 'h6 text-uppercase text-body-secondary small mb-3' }, 'Achievements & certifications'),
        h('ul', { class: 'list-unstyled mb-0' }, achievements.map(a => h('li', { class: 'card-soft p-4 mb-3 reveal d-flex gap-3' },
          h('span', { class: 'icon-tile' }, icon(a.kind === 'Certification' ? 'patch-check' : 'trophy')),
          h('div', { class: 'flex-grow-1' },
            h('div', { class: 'd-flex flex-wrap justify-content-between gap-2' },
              h('h4', { class: 'h6 mb-1' }, a.title),
              (a.dateLabel || a.date) && h('span', { class: 'small text-body-secondary' }, a.dateLabel || date(a.date))),
            a.issuer && h('p', { class: 'small fw-semibold text-brand mb-1' }, a.issuer),
            a.description && h('p', { class: 'small text-body-secondary mb-2' }, a.description),
            (a.verificationUrl || a.documentUrl) && h('div', { class: 'd-flex flex-wrap gap-3 small' },
              a.verificationUrl && externalLink(a.verificationUrl, [icon('patch-check'), ' Verify']),
              a.documentUrl && h('a', { href: a.documentUrl }, icon('file-earmark-arrow-down'), ' Certificate')))))))));
}

export function renderServices(data) {
  return h('div', { class: 'container' },
    sectionHeader('services', 'Services', 'How I can help', 'Engagements that match my day-to-day experience.'),
    h('div', { class: 'row g-4' }, data.services.map(s => h('div', { class: 'col-md-6 col-lg-4 reveal' },
      h('div', { class: 'card-soft card-hover p-4 h-100' },
        h('span', { class: 'icon-tile mb-3' }, icon(s.icon || 'code-slash')),
        h('h3', { class: 'h5' }, s.title),
        h('p', { class: 'text-body-secondary mb-0' }, s.description))))));
}

export function renderFooter(data) {
  const p = data.profile;
  const year = new Date().getFullYear();
  const nav = [['about', 'About'], ['skills', 'Skills'], ['experience', 'Experience'], ['projects', 'Projects'], ['contact', 'Contact']];
  return h('div', { class: 'container' },
    h('div', { class: 'row g-4 align-items-start' },
      h('div', { class: 'col-md-5' },
        h('p', { class: 'fw-bold fs-5 mb-1 text-body-emphasis' }, p.fullName),
        h('p', { class: 'text-body-secondary small mb-3' }, p.headline),
        socialLinks(p)),
      h('nav', { class: 'col-md-4', 'aria-label': 'Footer' },
        h('ul', { class: 'list-unstyled d-flex flex-wrap gap-3 mb-0' },
          nav.map(([id, label]) => h('li', null, h('a', { href: `/#${id}`, 'data-section-link': id }, label))))),
      h('div', { class: 'col-md-3 text-md-end d-flex flex-column gap-2 align-items-md-end' },
        p.email && h('a', { href: `mailto:${p.email}`, class: 'small' }, icon('envelope'), ' ', p.email),
        data.cvUrl && h('a', { href: data.cvUrl, download: '', class: 'small' }, icon('download'), ' Download CV'))),
    h('hr', { class: 'my-4' }),
    h('p', { class: 'small text-body-secondary mb-0' }, `© ${year} ${p.fullName}. All rights reserved.`));
}
