// Projects: list (generic resource page), full-page editor with screenshot management, and a public-identical preview.
import { h, icon } from '../../lib/dom.js';
import { api } from '../../lib/api.js';
import { toast } from '../../lib/toast.js';
import { resourcePage } from '../resource-page.js';
import { buildForm, withBusy, alertBox, confirmDialog, pageHeader, publishedBadge, iconButton, moveItem } from '../ui.js';
import { renderProjectDetail } from '../../shared/project-view.js';

const mediaUrl = id => `/api/media/${id}`;

const projectFields = [
  { type: 'section', name: 's1', label: 'Basics' },
  { name: 'title', label: 'Title', required: true, maxLength: 150, col: 'col-md-8' },
  { name: 'category', label: 'Category', maxLength: 80, col: 'col-md-4', placeholder: 'E-Commerce' },
  { name: 'slug', label: 'URL slug', maxLength: 120, col: 'col-md-6', help: 'Leave empty to generate from the title. Used in /projects/your-slug.',
    validate: v => (v && !/^[a-z0-9]+(?:-[a-z0-9]+)*$/.test(v) ? 'Use lowercase letters, numbers and single hyphens.' : null) },
  { name: 'role', label: 'Your role', maxLength: 200, col: 'col-md-6', placeholder: 'Backend developer' },
  { name: 'summary', label: 'Summary', type: 'textarea', rows: 2, required: true, maxLength: 400, help: 'Shown on project cards and in search results.' },
  { name: 'description', label: 'Detailed description', type: 'textarea', rows: 6, maxLength: 8000, help: 'Separate paragraphs with a blank line.' },
  { type: 'section', name: 's2', label: 'Details' },
  { name: 'technologies', label: 'Technologies', type: 'tags', help: 'Comma separated. Used for the technology filters.' },
  { name: 'features', label: 'Key features', type: 'list', help: 'One per line.' },
  { name: 'responsibilities', label: 'Your contributions', type: 'list', help: 'One per line.' },
  { type: 'section', name: 's3', label: 'Links & visibility' },
  { name: 'liveUrl', label: 'Live demo URL', type: 'url', maxLength: 500, col: 'col-md-6' },
  { name: 'sourceUrl', label: 'Source code URL', type: 'url', maxLength: 500, col: 'col-md-6' },
  { name: 'isFeatured', label: 'Featured (shown first, with a badge)', type: 'checkbox', col: 'col-md-6' },
  { name: 'isPublished', label: 'Published (visible on the portfolio)', type: 'checkbox', col: 'col-md-6' },
];

export const projectsListPage = ctx => resourcePage({
  title: 'Projects', singular: 'project', endpoint: 'projects', icon: 'kanban',
  description: 'Featured projects are listed first on the portfolio. Drafts are hidden from visitors.',
  itemTitle: p => p.title,
  itemMeta: p => [p.isFeatured && '★ Featured', p.category, p.technologies.slice(0, 4).join(', '), `${p.images.length} image${p.images.length === 1 ? '' : 's'}`].filter(Boolean).join(' · '),
  searchText: p => [p.title, p.category, p.summary, ...p.technologies].join(' '),
  editHref: p => `/admin/projects/${p.id}`,
  createHref: '/admin/projects/new',
  extraActions: p => [h('a', { class: 'btn btn-sm btn-light', href: `/admin/projects/${p.id}/preview`, 'data-nav': '', 'aria-label': `Preview "${p.title}"`, title: 'Preview' }, icon('eye'))],
}, ctx);

function imagesManager(project, onChange) {
  const grid = h('div', { class: 'row g-3' });
  const errorSlot = h('div');
  let images = [...project.images];

  async function saveOrder(next) {
    const previous = images;
    images = next;
    render();
    try {
      await api(`/api/admin/projects/${project.id}/images/reorder`, { method: 'PUT', body: { ids: images.map(i => i.id) } });
      onChange(images);
    } catch (err) { images = previous; render(); toast(err.message, 'danger'); }
  }

  function render() {
    if (!images.length) {
      grid.replaceChildren(h('div', { class: 'col-12' }, h('p', { class: 'text-body-secondary small mb-0' }, 'No screenshots yet. The first image becomes the project thumbnail.')));
      return;
    }
    grid.replaceChildren(...images.map((img, i) => {
      const caption = h('input', { class: 'form-control form-control-sm', value: img.caption ?? '', maxlength: 200, 'aria-label': `Caption for image ${i + 1}`, placeholder: 'Caption (optional)' });
      caption.addEventListener('change', async () => {
        try {
          await api(`/api/admin/projects/${project.id}/images/${img.id}`, { method: 'PATCH', body: { caption: caption.value } });
          img.caption = caption.value; toast('Caption saved.');
        } catch (err) { toast(err.message, 'danger'); }
      });
      return h('div', { class: 'col-sm-6 col-lg-4' },
        h('div', { class: 'image-tile' },
          i === 0 && h('span', { class: 'badge text-bg-primary cover-badge' }, 'Thumbnail'),
          h('img', { src: mediaUrl(img.mediaId), alt: img.caption || `Screenshot ${i + 1}`, loading: 'lazy' }),
          h('div', { class: 'p-2 d-grid gap-2' },
            caption,
            h('div', { class: 'd-flex gap-1 justify-content-between' },
              h('div', { class: 'd-flex gap-1' },
                iconButton('arrow-left', 'Move image earlier', () => saveOrder(moveItem(images, i, -1))),
                iconButton('arrow-right', 'Move image later', () => saveOrder(moveItem(images, i, 1))),
                i > 0 && h('button', { type: 'button', class: 'btn btn-sm btn-light', onclick: () => saveOrder([img, ...images.filter(x => x !== img)]) }, 'Set as thumbnail')),
              iconButton('trash', 'Delete image', async () => {
                if (!await confirmDialog({ title: 'Delete image?', message: 'This screenshot will be permanently deleted.' })) return;
                try {
                  await api(`/api/admin/projects/${project.id}/images/${img.id}`, { method: 'DELETE' });
                  images = images.filter(x => x !== img); render(); onChange(images); toast('Image deleted.');
                } catch (err) { toast(err.message, 'danger'); }
              }, 'btn-light text-danger')))));
    }));
  }

  const fileInput = h('input', { type: 'file', class: 'form-control', id: 'project-image-file', accept: 'image/png,image/jpeg,image/webp', 'aria-describedby': 'project-image-help' });
  const captionInput = h('input', { type: 'text', class: 'form-control', id: 'project-image-caption', maxlength: 200, placeholder: 'Caption (optional)' });
  const uploadBtn = h('button', { type: 'submit', class: 'btn btn-outline-primary' }, icon('upload'), ' Upload');
  const uploadForm = h('form', { class: 'row g-2 align-items-end mb-3' },
    h('div', { class: 'col-md-5' }, h('label', { class: 'form-label small fw-medium', for: 'project-image-file' }, 'Screenshot'), fileInput),
    h('div', { class: 'col-md-5' }, h('label', { class: 'form-label small fw-medium', for: 'project-image-caption' }, 'Caption'), captionInput),
    h('div', { class: 'col-md-2 d-grid' }, uploadBtn),
    h('div', { class: 'col-12 form-text', id: 'project-image-help' }, 'PNG, JPG or WebP, up to 5 MB. Up to 12 images.'));
  uploadForm.addEventListener('submit', async e => {
    e.preventDefault();
    errorSlot.replaceChildren();
    const file = fileInput.files[0];
    if (!file) return errorSlot.replaceChildren(alertBox('Choose an image first.'));
    if (file.size > 5 * 1024 * 1024) return errorSlot.replaceChildren(alertBox('Images must be 5 MB or smaller.'));
    const form = new FormData();
    form.append('file', file);
    form.append('caption', captionInput.value);
    try {
      const image = await withBusy(uploadBtn, () => api(`/api/admin/projects/${project.id}/images`, { method: 'POST', form }), 'Uploading…');
      images.push(image); render(); onChange(images);
      uploadForm.reset();
      toast('Image uploaded.');
    } catch (err) { errorSlot.replaceChildren(alertBox(err.message)); }
  });

  render();
  return h('section', { class: 'panel mt-4', 'aria-labelledby': 'images-title' },
    h('div', { class: 'panel-header' }, h('h2', { class: 'h5 mb-0', id: 'images-title' }, 'Screenshots')),
    h('div', { class: 'panel-body' }, errorSlot, uploadForm, grid));
}

/** /admin/projects/new and /admin/projects/:id */
export async function projectEditorPage(ctx, id) {
  const isNew = id == null;
  let project = { technologies: [], features: [], responsibilities: [], images: [], isPublished: false, isFeatured: false };
  if (!isNew) project = await api(`/api/admin/projects/${id}`);

  const built = buildForm(projectFields, project);
  const errorSlot = h('div');
  const saveBtn = h('button', { type: 'submit', class: 'btn btn-primary' }, icon('check-lg'), isNew ? ' Create project' : ' Save changes');
  built.form.id = 'project-form';
  saveBtn.setAttribute('form', 'project-form');
  const statusBadge = h('span', null, isNew ? null : publishedBadge(project.isPublished));

  built.form.addEventListener('submit', async e => {
    e.preventDefault();
    errorSlot.replaceChildren();
    if (!built.validate()) return;
    try {
      const saved = await withBusy(saveBtn, () => api(isNew ? '/api/admin/projects' : `/api/admin/projects/${id}`, { method: isNew ? 'POST' : 'PUT', body: built.getValues() }));
      toast(isNew ? 'Project created. You can add screenshots now.' : 'Project saved.');
      if (isNew) ctx.navigate(`/admin/projects/${saved.id}`, { replace: true });
      else { project = { ...saved }; statusBadge.replaceChildren(publishedBadge(saved.isPublished)); }
    } catch (err) {
      built.showServerErrors(err);
      errorSlot.replaceChildren(alertBox(err.message));
      errorSlot.scrollIntoView({ block: 'center' });
    }
  });

  return h('div', null,
    h('a', { href: '/admin/projects', 'data-nav': '', class: 'link-secondary text-decoration-none d-inline-flex align-items-center gap-1 mb-3' }, icon('arrow-left'), 'Projects'),
    pageHeader(isNew ? 'New project' : project.title, isNew ? 'Save the project first, then add screenshots.' : h('span', null, statusBadge, ' ', h('span', { class: 'small' }, `/projects/${project.slug}`)),
      !isNew && h('a', { class: 'btn btn-outline-primary', href: `/admin/projects/${id}/preview`, 'data-nav': '' }, icon('eye'), ' Preview')),
    errorSlot,
    h('div', { class: 'panel' }, h('div', { class: 'panel-body' }, built.form)),
    !isNew && imagesManager(project, images => { project.images = images; }),
    h('div', { class: 'sticky-actions d-flex gap-2 justify-content-end' },
      h('a', { class: 'btn btn-light', href: '/admin/projects', 'data-nav': '' }, 'Back'), saveBtn));
}

/** Renders the project exactly as visitors will see it, including drafts. */
export async function projectPreviewPage(ctx, id) {
  const project = await api(`/api/admin/projects/${id}`);
  const view = { ...project, images: project.images.map(i => ({ url: mediaUrl(i.mediaId), caption: i.caption })) };
  const banner = h('div', { class: 'preview-banner p-3 mb-4 d-flex flex-wrap gap-2 justify-content-between align-items-center' },
    h('span', null, icon('eye'), ' Preview — ', project.isPublished ? 'this project is published.' : 'this project is a draft and hidden from visitors.'),
    h('div', { class: 'd-flex gap-2' },
      h('a', { class: 'btn btn-sm btn-primary', href: `/admin/projects/${id}`, 'data-nav': '' }, icon('pencil'), ' Edit'),
      project.isPublished && h('a', { class: 'btn btn-sm btn-outline-primary', href: `/projects/${project.slug}`, target: '_blank', rel: 'noopener' }, icon('box-arrow-up-right'), ' Open live page')));
  return h('div', { class: 'bg-body rounded-4 py-4' }, renderProjectDetail(view, { backHref: `/admin/projects/${id}`, backLabel: 'Back to editor', banner }));
}
