// Document library: CVs, cover letters and certificates with public/private visibility and a primary public CV.
import { h, icon, copyText } from '../../lib/dom.js';
import { api } from '../../lib/api.js';
import { toast } from '../../lib/toast.js';
import { bytes, dateTime } from '../../lib/format.js';
import { buildForm, withBusy, alertBox, openFormModal, confirmDialog, pageHeader, emptyState, loading, errorState, iconButton } from '../ui.js';

const KINDS = [{ value: 'Cv', label: 'CV / Résumé' }, { value: 'CoverLetter', label: 'Cover letter' }, { value: 'Certificate', label: 'Certificate' }, { value: 'Other', label: 'Other' }];
const kindLabel = k => KINDS.find(x => x.value === k)?.label ?? k;
const ACCEPT = '.pdf,.docx,.png,.jpg,.jpeg,.webp';
const MAX = 10 * 1024 * 1024;

const metaFields = [
  { name: 'title', label: 'Title', required: true, maxLength: 150, col: 'col-md-8' },
  { name: 'version', label: 'Version', maxLength: 30, col: 'col-md-4', placeholder: '2026-10' },
  { name: 'kind', label: 'Type', type: 'select', options: KINDS, col: 'col-md-6' },
  { name: 'description', label: 'Description', type: 'textarea', rows: 2, maxLength: 1000 },
  { name: 'isPublic', label: 'Public — downloadable by visitors', type: 'checkbox' },
  { name: 'isPrimary', label: 'Primary CV — served by the "Download CV" buttons', type: 'checkbox', showIf: v => v.kind === 'Cv' && v.isPublic },
];

function validateFile(file) {
  if (!file) return 'Choose a file to upload.';
  if (file.size > MAX) return 'Files must be 10 MB or smaller.';
  if (!/\.(pdf|docx|png|jpe?g|webp)$/i.test(file.name)) return 'Allowed types: PDF, DOCX, PNG, JPG, WebP.';
  return null;
}

export function documentsPage() {
  const listEl = h('div', { class: 'panel' }, loading());
  let docs = [];

  // ---- Upload form ----
  const fileInput = h('input', { type: 'file', class: 'form-control', id: 'doc-file', name: 'file', accept: ACCEPT, 'aria-describedby': 'doc-file-help' });
  const built = buildForm(metaFields, { kind: 'Cv', isPublic: false, isPrimary: true });
  const uploadError = h('div');
  const uploadBtn = h('button', { type: 'submit', class: 'btn btn-primary' }, icon('upload'), ' Upload document');
  built.form.prepend(h('div', { class: 'mb-3' },
    h('label', { class: 'form-label fw-medium', for: 'doc-file' }, 'File', h('span', { class: 'text-danger', 'aria-hidden': 'true' }, ' *')),
    fileInput, h('div', { class: 'form-text', id: 'doc-file-help' }, 'PDF, DOCX, PNG, JPG or WebP — up to 10 MB. Files are stored privately on the server.')));
  built.form.append(h('div', { class: 'd-flex justify-content-end mt-3' }, uploadBtn));
  fileInput.addEventListener('change', () => {
    const title = built.form.elements.namedItem('title');
    if (!title.value && fileInput.files[0]) title.value = fileInput.files[0].name.replace(/\.[^.]+$/, '').replace(/[_-]+/g, ' ');
  });
  built.form.addEventListener('submit', async e => {
    e.preventDefault();
    uploadError.replaceChildren();
    const fileError = validateFile(fileInput.files[0]);
    if (fileError) { uploadError.replaceChildren(alertBox(fileError)); fileInput.focus(); return; }
    if (!built.validate()) return;
    const form = new FormData();
    form.append('file', fileInput.files[0]);
    for (const [k, v] of Object.entries(built.getValues())) form.append(k, v ?? '');
    try {
      await withBusy(uploadBtn, () => api('/api/admin/documents', { method: 'POST', form }), 'Uploading…');
      toast('Document uploaded.');
      built.form.reset();
      load();
    } catch (err) { built.showServerErrors(err); uploadError.replaceChildren(alertBox(err.message)); }
  });

  async function editMeta(doc) {
    const saved = await openFormModal({
      title: 'Edit document', fields: metaFields, values: doc,
      onSubmit: v => api(`/api/admin/documents/${doc.id}`, { method: 'PUT', body: v }),
    });
    if (saved) { toast('Document updated.'); load(); }
  }

  async function remove(doc) {
    if (!await confirmDialog({ title: 'Delete document?', message: `"${doc.title}" (${doc.originalFileName}) will be permanently deleted from the server.` })) return;
    try { await api(`/api/admin/documents/${doc.id}`, { method: 'DELETE' }); toast('Document deleted.'); load(); }
    catch (err) { toast(err.message, 'danger'); }
  }

  function render() {
    if (!docs.length) { listEl.replaceChildren(emptyState('folder2-open', 'No documents uploaded yet.')); return; }
    listEl.replaceChildren(...docs.map(doc => h('div', { class: 'item-row' },
      h('span', { class: 'icon-tile' }, icon(doc.contentType === 'application/pdf' ? 'file-earmark-pdf' : doc.contentType.startsWith('image/') ? 'file-earmark-image' : 'file-earmark-word')),
      h('div', { class: 'item-main' },
        h('div', { class: 'd-flex flex-wrap gap-2 align-items-center' },
          h('span', { class: 'item-title' }, doc.title),
          doc.isPublic ? h('span', { class: 'badge text-bg-success' }, icon('globe2'), ' Public') : h('span', { class: 'badge text-bg-secondary' }, icon('lock'), ' Private'),
          doc.isPrimary && h('span', { class: 'badge text-bg-primary' }, 'Primary CV')),
        h('div', { class: 'item-meta' }, [kindLabel(doc.kind), doc.version && `v${doc.version}`, doc.originalFileName, bytes(doc.sizeBytes), `Updated ${dateTime(doc.updatedAt)}`].filter(Boolean).join(' · '))),
      h('div', { class: 'item-actions' },
        h('a', { class: 'btn btn-sm btn-light', href: `/api/admin/documents/${doc.id}/download`, 'aria-label': `Download ${doc.title}`, title: 'Download' }, icon('download')),
        iconButton('arrow-repeat', `Replace file for ${doc.title}`, () => replaceFileModal(doc)),
        iconButton('pencil', `Edit ${doc.title}`, () => editMeta(doc)),
        iconButton('trash', `Delete ${doc.title}`, () => remove(doc), 'btn-light text-danger')))));
  }

  /** Replace-file modal built directly so it can include a file input. */
  function replaceFileModal(doc) {
    const input = h('input', { type: 'file', class: 'form-control', accept: ACCEPT, id: `replace-${doc.id}` });
    const version = h('input', { type: 'text', class: 'form-control', maxlength: 30, id: `replace-version-${doc.id}`, placeholder: doc.version ?? '' });
    const errorSlot = h('div');
    const submit = h('button', { type: 'submit', class: 'btn btn-primary' }, icon('upload'), ' Replace');
    const form = h('form', { novalidate: true },
      h('div', { class: 'modal-header' }, h('h2', { class: 'modal-title h5', id: `replace-title-${doc.id}` }, 'Replace file'),
        h('button', { type: 'button', class: 'btn-close', 'data-bs-dismiss': 'modal', 'aria-label': 'Close' })),
      h('div', { class: 'modal-body' },
        h('p', { class: 'small text-body-secondary' }, `"${doc.title}" keeps its title, visibility and public links, so the Download CV link updates automatically.`),
        errorSlot,
        h('label', { class: 'form-label fw-medium', for: input.id }, 'New file'), input,
        h('label', { class: 'form-label fw-medium mt-3', for: version.id }, 'New version label (optional)'), version),
      h('div', { class: 'modal-footer' }, h('button', { type: 'button', class: 'btn btn-light', 'data-bs-dismiss': 'modal' }, 'Cancel'), submit));
    const modal = h('div', { class: 'modal fade', tabindex: '-1', 'aria-labelledby': `replace-title-${doc.id}`, role: 'dialog', 'aria-modal': 'true' },
      h('div', { class: 'modal-dialog' }, h('div', { class: 'modal-content' }, form)));
    document.body.append(modal);
    const instance = new window.bootstrap.Modal(modal);
    modal.addEventListener('hidden.bs.modal', () => { instance.dispose(); modal.remove(); });
    form.addEventListener('submit', async e => {
      e.preventDefault();
      const fileError = validateFile(input.files[0]);
      if (fileError) { errorSlot.replaceChildren(alertBox(fileError)); return; }
      const data = new FormData();
      data.append('file', input.files[0]);
      if (version.value.trim()) data.append('version', version.value.trim());
      try {
        await withBusy(submit, () => api(`/api/admin/documents/${doc.id}/file`, { method: 'POST', form: data }), 'Uploading…');
        instance.hide(); toast('File replaced.'); load();
      } catch (err) { errorSlot.replaceChildren(alertBox(err.message)); }
    });
    instance.show();
  }

  async function load() {
    try { docs = await api('/api/admin/documents'); render(); }
    catch (err) { listEl.replaceChildren(errorState(err.message, load)); }
  }
  load();

  const cvLink = `${location.origin}/api/public/cv`;
  return h('div', null,
    pageHeader('Documents', 'Your CV, cover letters and certificates. Only documents marked Public can be downloaded by visitors.'),
    h('div', { class: 'row g-4' },
      h('div', { class: 'col-xl-5' },
        h('section', { class: 'panel mb-4' },
          h('div', { class: 'panel-header' }, h('h2', { class: 'h6 mb-0' }, 'Upload a document')),
          h('div', { class: 'panel-body' }, uploadError, built.form)),
        h('section', { class: 'panel' },
          h('div', { class: 'panel-body small' },
            h('p', { class: 'fw-semibold mb-1' }, icon('link-45deg'), ' Permanent CV link'),
            h('p', { class: 'text-body-secondary mb-2' }, 'Always serves your primary public CV (or the newest public CV).'),
            h('div', { class: 'input-group input-group-sm' },
              h('input', { class: 'form-control', value: cvLink, readonly: true, 'aria-label': 'Public CV link' }),
              h('button', { class: 'btn btn-outline-primary', type: 'button', onclick: async () => { try { await copyText(cvLink); toast('Link copied.'); } catch { toast('Copy failed.', 'danger'); } } }, icon('clipboard'), ' Copy'))))),
      h('div', { class: 'col-xl-7' }, listEl)));
}
