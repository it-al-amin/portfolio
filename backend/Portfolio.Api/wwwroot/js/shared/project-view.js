// Project detail view, shared by the public site and the admin "Preview" page so previews match exactly.
import { h, icon, externalLink, initials } from '../lib/dom.js';
import { paragraphs } from '../lib/format.js';

/**
 * @typedef {{ url: string, caption?: string }} ProjectImage
 * @typedef {{ title: string, slug?: string, category?: string, summary: string, description?: string, role?: string,
 *   technologies: string[], features: string[], responsibilities: string[], liveUrl?: string, sourceUrl?: string,
 *   isFeatured?: boolean, images: ProjectImage[] }} ProjectView
 */

/** Thumbnail image or a generated fallback tile. */
export function projectThumb(project, { eager = false } = {}) {
  const cover = project.images?.[0];
  if (cover) {
    return h('img', {
      class: 'project-thumb', src: cover.url, alt: cover.caption || `${project.title} screenshot`,
      loading: eager ? 'eager' : 'lazy', decoding: 'async', width: 640, height: 360,
    });
  }
  return h('div', { class: 'project-thumb-fallback', 'aria-hidden': 'true' }, initials(project.title));
}

export function projectLinks(project, size = '') {
  return [
    project.liveUrl && externalLink(project.liveUrl, [icon('box-arrow-up-right'), ' Live demo'], { class: `btn btn-primary ${size}` }),
    project.sourceUrl && externalLink(project.sourceUrl, [icon('github'), ' Source code'], { class: `btn btn-outline-primary ${size}` }),
  ].filter(Boolean);
}

function listBlock(title, items, iconName) {
  if (!items?.length) return null;
  return h('section', { class: 'mb-4' },
    h('h2', { class: 'h5 d-flex align-items-center gap-2 mb-3' }, icon(iconName, 'text-brand'), title),
    h('ul', { class: 'detail-list' }, items.map(item => h('li', null, item))));
}

/**
 * @param {ProjectView} project
 * @param {{ backHref?: string, backLabel?: string, onBack?: (e: Event) => void, banner?: Node }} [options]
 */
export function renderProjectDetail(project, { backHref = '/#projects', backLabel = 'All projects', onBack, banner } = {}) {
  const gallery = project.images?.length
    ? h('div', { class: 'row g-3 project-gallery mb-5' },
        project.images.map((img, i) => h('figure', { class: `col-12 ${project.images.length > 1 ? 'col-md-6' : ''} m-0` },
          h('a', { href: img.url, target: '_blank', rel: 'noopener', 'aria-label': `Open full-size image ${i + 1}` },
            h('img', { src: img.url, alt: img.caption || `${project.title} screenshot ${i + 1}`, loading: i ? 'lazy' : 'eager', width: 800, height: 500 })),
          img.caption && h('figcaption', { class: 'small text-body-secondary mt-2' }, img.caption))))
    : null;

  return h('article', { class: 'container' },
    banner,
    h('a', { href: backHref, class: 'link-secondary text-decoration-none d-inline-flex align-items-center gap-1 mb-4', onclick: onBack },
      icon('arrow-left'), backLabel),
    h('header', { class: 'mb-4' },
      h('div', { class: 'd-flex flex-wrap gap-2 mb-2' },
        project.category && h('span', { class: 'badge rounded-pill text-bg-light border' }, project.category),
        project.isFeatured && h('span', { class: 'badge rounded-pill featured-badge' }, icon('star-fill'), ' Featured')),
      h('h1', { class: 'display-6 mb-3' }, project.title),
      h('p', { class: 'lead text-body-secondary mb-4' }, project.summary),
      h('div', { class: 'd-flex flex-wrap gap-2' }, projectLinks(project))),
    gallery,
    h('div', { class: 'row g-5' },
      h('div', { class: 'col-lg-8' },
        project.description && h('section', { class: 'mb-4' },
          h('h2', { class: 'h5 mb-3' }, 'Overview'),
          paragraphs(project.description).map(p => h('p', { class: 'text-body-secondary' }, p))),
        listBlock('Key features', project.features, 'stars'),
        listBlock('My contributions', project.responsibilities, 'person-workspace')),
      h('aside', { class: 'col-lg-4' },
        h('div', { class: 'card-soft p-4' },
          project.role && [h('h2', { class: 'h6 text-uppercase text-body-secondary small mb-2' }, 'Role'), h('p', null, project.role)],
          h('h2', { class: 'h6 text-uppercase text-body-secondary small mb-3' }, 'Technologies'),
          project.technologies?.length
            ? h('ul', { class: 'chip-list' }, project.technologies.map(t => h('li', { class: 'tag' }, t)))
            : h('p', { class: 'text-body-secondary small mb-0' }, 'Not specified')))));
}
