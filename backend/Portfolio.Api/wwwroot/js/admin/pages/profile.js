// Profile editor (identity, bio, contact, links, photo) and Settings (SEO + password).
import { h, icon, initials } from '../../lib/dom.js';
import { api } from '../../lib/api.js';
import { toast } from '../../lib/toast.js';
import { buildForm, withBusy, alertBox, confirmDialog, pageHeader } from '../ui.js';

/** Profile fields that PUT /api/admin/profile accepts; used to send the full object when only some fields are edited. */
const PROFILE_KEYS = ['fullName', 'headline', 'tagline', 'summary', 'bio', 'careerInterests', 'highlights', 'location', 'publicEmail', 'showEmail',
  'phone', 'showPhone', 'whatsAppNumber', 'gitHubUrl', 'linkedInUrl', 'websiteUrl', 'availabilityStatus', 'showAvailability', 'seoTitle', 'seoDescription', 'siteUrl'];
const pick = obj => Object.fromEntries(PROFILE_KEYS.map(k => [k, obj[k] ?? null]));

export const contactFields = [
  { type: 'section', name: 'c', label: 'Public contact options' },
  { name: 'publicEmail', label: 'Public contact email', type: 'email', maxLength: 254, col: 'col-md-8' },
  { name: 'showEmail', label: 'Show email publicly', type: 'checkbox', col: 'col-md-4 d-flex align-items-end pb-2' },
  { name: 'phone', label: 'Phone', type: 'tel', maxLength: 30, col: 'col-md-8' },
  { name: 'showPhone', label: 'Show phone publicly', type: 'checkbox', col: 'col-md-4 d-flex align-items-end pb-2' },
  { name: 'whatsAppNumber', label: 'WhatsApp number', maxLength: 20, help: 'Digits only, international format without + (e.g. 8801XXXXXXXXX). Leave empty to hide the WhatsApp button.',
    validate: v => (v && !/^[0-9]{8,15}$/.test(v) ? 'Use 8–15 digits with no spaces or +.' : null) },
  { type: 'section', name: 'l', label: 'Links' },
  { name: 'gitHubUrl', label: 'GitHub URL', type: 'url', maxLength: 500, col: 'col-md-6' },
  { name: 'linkedInUrl', label: 'LinkedIn URL', type: 'url', maxLength: 500, col: 'col-md-6' },
  { name: 'websiteUrl', label: 'Other website', type: 'url', maxLength: 500, col: 'col-md-6' },
];

const profileFields = [
  { type: 'section', name: 'i', label: 'Identity' },
  { name: 'fullName', label: 'Full name', required: true, maxLength: 120, col: 'col-md-6' },
  { name: 'headline', label: 'Professional title', required: true, maxLength: 160, col: 'col-md-6' },
  { name: 'tagline', label: 'Tagline', maxLength: 200 },
  { name: 'location', label: 'Location', maxLength: 120, col: 'col-md-6' },
  { name: 'availabilityStatus', label: 'Availability / career status', maxLength: 120, col: 'col-md-6', placeholder: 'Open to new opportunities' },
  { name: 'showAvailability', label: 'Show availability badge in the hero', type: 'checkbox' },
  { type: 'section', name: 'a', label: 'About' },
  { name: 'summary', label: 'Professional summary', type: 'textarea', rows: 3, maxLength: 1200, help: 'Shown in the hero section.' },
  { name: 'bio', label: 'Biography', type: 'textarea', rows: 8, maxLength: 6000, help: 'Shown in About me. Separate paragraphs with a blank line.' },
  { name: 'careerInterests', label: 'Technical focus / career interests', type: 'list', rows: 4, help: 'One per line.' },
  { name: 'highlights', label: 'Hero highlights', type: 'list', rows: 3, help: 'Up to 6 lines formatted as "value | label", e.g. "1,600+ | Problems solved".',
    validate: v => (v?.length > 6 ? 'Use at most 6 highlights.' : v?.some(x => !x.includes('|')) ? 'Each line needs a "|" between value and label.' : null) },
  ...contactFields,
];

function photoPanel(profile) {
  const preview = h('div');
  const errorSlot = h('div');
  const renderPreview = () => preview.replaceChildren(profile.profileImageId
    ? h('img', { class: 'avatar-preview', src: `/api/media/${profile.profileImageId}`, alt: 'Current profile photo' })
    : h('div', { class: 'avatar-fallback', 'aria-label': 'No photo — initials are shown instead', role: 'img' }, initials(profile.fullName)));

  const input = h('input', { type: 'file', class: 'form-control', id: 'profile-photo', accept: 'image/png,image/jpeg,image/webp' });
  const upload = h('button', { type: 'button', class: 'btn btn-outline-primary' }, icon('upload'), ' Upload photo');
  const removeBtn = h('button', { type: 'button', class: 'btn btn-light text-danger' }, icon('trash'), ' Remove');

  upload.addEventListener('click', async () => {
    errorSlot.replaceChildren();
    const file = input.files[0];
    if (!file) return errorSlot.replaceChildren(alertBox('Choose an image first.'));
    if (file.size > 5 * 1024 * 1024) return errorSlot.replaceChildren(alertBox('Images must be 5 MB or smaller.'));
    const form = new FormData();
    form.append('file', file);
    try {
      const updated = await withBusy(upload, () => api('/api/admin/profile/image', { method: 'POST', form }), 'Uploading…');
      profile.profileImageId = updated.profileImageId;
      input.value = '';
      renderPreview();
      toast('Profile photo updated.');
    } catch (err) { errorSlot.replaceChildren(alertBox(err.message)); }
  });
  removeBtn.addEventListener('click', async () => {
    if (!profile.profileImageId || !await confirmDialog({ title: 'Remove photo?', message: 'Your initials will be shown instead.', confirmLabel: 'Remove' })) return;
    try {
      await api('/api/admin/profile/image', { method: 'DELETE' });
      profile.profileImageId = null; renderPreview(); toast('Photo removed.');
    } catch (err) { toast(err.message, 'danger'); }
  });

  renderPreview();
  return h('section', { class: 'panel mb-4', 'aria-labelledby': 'photo-title' },
    h('div', { class: 'panel-header' }, h('h2', { class: 'h6 mb-0', id: 'photo-title' }, 'Profile photo')),
    h('div', { class: 'panel-body d-flex flex-wrap gap-4 align-items-center' },
      preview,
      h('div', { class: 'flex-grow-1' }, errorSlot,
        h('label', { class: 'form-label fw-medium', for: 'profile-photo' }, 'New photo'), input,
        h('div', { class: 'form-text mb-2' }, 'Square PNG, JPG or WebP, up to 5 MB. 600×600 px or larger looks best.'),
        h('div', { class: 'd-flex gap-2' }, upload, removeBtn))));
}

/** Form panel that saves a subset of profile fields while sending the full profile object. */
export function profileFormPanel(profile, fields, { title, submitLabel = 'Save changes', onSaved } = {}) {
  const built = buildForm(fields, profile);
  const errorSlot = h('div');
  const save = h('button', { type: 'submit', class: 'btn btn-primary' }, icon('check-lg'), ` ${submitLabel}`);
  built.form.append(h('div', { class: 'd-flex justify-content-end mt-4' }, save));
  built.form.addEventListener('submit', async e => {
    e.preventDefault();
    errorSlot.replaceChildren();
    if (!built.validate()) return;
    try {
      const updated = await withBusy(save, () => api('/api/admin/profile', { method: 'PUT', body: { ...pick(profile), ...built.getValues() } }));
      Object.assign(profile, updated);
      toast('Saved. Changes are live on the portfolio.');
      onSaved?.(updated);
    } catch (err) { built.showServerErrors(err); errorSlot.replaceChildren(alertBox(err.message)); }
  });
  return h('section', { class: 'panel mb-4' },
    title && h('div', { class: 'panel-header' }, h('h2', { class: 'h6 mb-0' }, title)),
    h('div', { class: 'panel-body' }, errorSlot, built.form));
}

export async function profilePage() {
  const profile = await api('/api/admin/profile');
  return h('div', null,
    pageHeader('Profile', 'Your name, introduction, contact details and links.',
      h('a', { class: 'btn btn-outline-primary', href: '/', target: '_blank', rel: 'noopener' }, icon('box-arrow-up-right'), ' View site')),
    photoPanel(profile),
    profileFormPanel(profile, profileFields));
}

const seoFields = [
  { name: 'seoTitle', label: 'Page title', maxLength: 70, help: 'Shown in browser tabs and search results. Up to 70 characters.' },
  { name: 'seoDescription', label: 'Meta description', type: 'textarea', rows: 3, maxLength: 170, help: 'Shown in search results and link previews. Up to 170 characters.' },
  { name: 'siteUrl', label: 'Public site URL', type: 'url', maxLength: 500, help: 'e.g. https://yourname.dev — used for canonical links, Open Graph and sitemap.xml.' },
];

const passwordFields = [
  { name: 'currentPassword', label: 'Current password', type: 'password', autocomplete: 'current-password', required: true, maxLength: 200 },
  { name: 'newPassword', label: 'New password', type: 'password', autocomplete: 'new-password', required: true, maxLength: 200, help: 'At least 8 characters.',
    validate: v => (v && v.length < 8 ? 'Use at least 8 characters.' : null) },
  { name: 'confirmPassword', label: 'Confirm new password', type: 'password', autocomplete: 'new-password', required: true,
    validate: (v, all) => (v !== all.newPassword ? 'Passwords do not match.' : null) },
];

export async function settingsPage(ctx) {
  const profile = await api('/api/admin/profile');

  const built = buildForm(passwordFields, {});
  const errorSlot = h('div');
  const save = h('button', { type: 'submit', class: 'btn btn-primary' }, 'Change password');
  built.form.append(h('div', { class: 'd-flex justify-content-end mt-4' }, save));
  built.form.addEventListener('submit', async e => {
    e.preventDefault();
    errorSlot.replaceChildren();
    if (!built.validate()) return;
    const { currentPassword, newPassword } = built.getValues();
    try {
      await withBusy(save, () => api('/api/auth/change-password', { method: 'POST', body: { currentPassword, newPassword } }));
      toast('Password changed. Please sign in again.');
      ctx.navigate('/admin/login');
    } catch (err) { built.showServerErrors(err); errorSlot.replaceChildren(alertBox(err.message)); }
  });

  return h('div', null,
    pageHeader('Settings', 'Search engine settings and account security.',
      h('a', { class: 'btn btn-outline-primary', href: '/admin/settings/email' }, icon('envelope-at'), ' Email settings')),
    profileFormPanel(profile, seoFields, { title: 'SEO & sharing' }),
    h('section', { class: 'panel mb-4' },
      h('div', { class: 'panel-header' }, h('h2', { class: 'h6 mb-0' }, 'Change password')),
      h('div', { class: 'panel-body' }, h('p', { class: 'small text-body-secondary' }, 'Changing your password signs out all existing sessions.'), errorSlot, built.form)));
}
