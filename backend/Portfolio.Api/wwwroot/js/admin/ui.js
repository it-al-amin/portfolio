// Admin UI building blocks: declarative forms, modal editors, confirmation dialogs and page scaffolding.
import { h, icon } from '../lib/dom.js';
import { rules, validate, showErrors, clearErrors } from '../lib/validate.js';

let uid = 0;

/**
 * @typedef {{ name: string, label?: string, type?: 'text'|'email'|'url'|'tel'|'textarea'|'list'|'tags'|'month'|'date'|'number'|'checkbox'|'select'|'section',
 *   required?: boolean, maxLength?: number, min?: number, max?: number, rows?: number, help?: string, placeholder?: string,
 *   options?: Array<{ value: string, label: string }>, col?: string, showIf?: (values: Record<string, any>) => boolean,
 *   validate?: (value: any, values: Record<string, any>) => string|null }} FieldDef
 *
 * Value conventions: 'list' = one item per line -> string[]; 'tags' = comma separated -> string[];
 * 'month' = YYYY-MM input stored as YYYY-MM-01; empty optional values become null.
 */

function toInputValue(field, value) {
  if (value == null) return '';
  if (field.type === 'list') return (value || []).join('\n');
  if (field.type === 'tags') return (value || []).join(', ');
  if (field.type === 'month') return String(value).slice(0, 7);
  return String(value);
}

function fromInputValue(field, control) {
  if (field.type === 'checkbox') return control.checked;
  const raw = control.value;
  if (field.type === 'list') return raw.split('\n').map(s => s.trim()).filter(Boolean);
  if (field.type === 'tags') return raw.split(',').map(s => s.trim()).filter(Boolean);
  if (raw.trim() === '') return field.required ? '' : null;
  if (field.type === 'number') return Number(raw);
  if (field.type === 'month') return `${raw}-01`;
  return field.type === 'password' ? raw : raw.trim();
}

/**
 * Build a Bootstrap form from field definitions.
 * @param {FieldDef[]} fields
 * @param {Record<string, any>} [values]
 */
export function buildForm(fields, values = {}) {
  const prefix = `f${++uid}`;
  const form = h('form', { novalidate: true });
  const row = h('div', { class: 'row g-3' });
  const wrappers = new Map();

  for (const field of fields) {
    if (field.type === 'section') {
      row.append(h('div', { class: 'col-12 pt-2' }, h('h3', { class: 'h6 text-uppercase text-body-secondary small mb-0' }, field.label)));
      continue;
    }
    const id = `${prefix}-${field.name}`;
    const helpId = field.help ? `${id}-help` : null;
    const value = values[field.name];
    let control;
    let wrapper;

    if (field.type === 'checkbox') {
      control = h('input', { class: 'form-check-input', type: 'checkbox', role: 'switch', id, name: field.name });
      control.checked = Boolean(value);
      wrapper = h('div', { class: field.col ?? 'col-12' },
        h('div', { class: 'form-check form-switch' }, control, h('label', { class: 'form-check-label', for: id }, field.label)),
        helpId && h('div', { class: 'form-text', id: helpId }, field.help));
    } else {
      if (field.type === 'select') {
        control = h('select', { class: 'form-select', id, name: field.name },
          field.options.map(o => h('option', { value: o.value }, o.label)));
        control.value = value == null ? field.options[0]?.value ?? '' : String(value);
      } else if (field.type === 'textarea' || field.type === 'list') {
        control = h('textarea', { class: 'form-control', id, name: field.name, rows: field.rows ?? (field.type === 'list' ? 5 : 4), placeholder: field.placeholder });
        control.value = toInputValue(field, value);
      } else {
        const type = { tags: 'text', month: 'month', date: 'date', number: 'number', email: 'email', url: 'url', tel: 'tel', password: 'password' }[field.type] ?? 'text';
        control = h('input', { class: 'form-control', type, id, name: field.name, placeholder: field.placeholder, min: field.min, max: field.max, autocomplete: field.autocomplete });
        control.value = toInputValue(field, value);
      }
      if (field.maxLength && field.type !== 'list' && field.type !== 'tags') control.maxLength = field.maxLength;
      if (field.required) control.required = true;
      wrapper = h('div', { class: field.col ?? 'col-12' },
        h('label', { class: 'form-label fw-medium', for: id }, field.label, field.required ? h('span', { class: 'text-danger', 'aria-hidden': 'true' }, ' *') : null),
        control,
        helpId && h('div', { class: 'form-text', id: helpId }, field.help));
    }
    if (helpId) {
      control.setAttribute('aria-describedby', helpId);
      control.setAttribute('data-help-id', helpId);
    }
    wrappers.set(field.name, wrapper);
    row.append(wrapper);
  }
  form.append(row);

  const getValues = () => {
    const out = {};
    for (const field of fields) {
      if (field.type === 'section') continue;
      const control = form.elements.namedItem(field.name);
      out[field.name] = fromInputValue(field, control);
    }
    return out;
  };

  const updateVisibility = () => {
    const current = getValues();
    for (const field of fields) if (field.showIf) wrappers.get(field.name).hidden = !field.showIf(current);
  };
  form.addEventListener('input', updateVisibility);
  form.addEventListener('change', updateVisibility);
  updateVisibility();

  const schema = {};
  for (const field of fields) {
    if (field.type === 'section' || field.type === 'checkbox') continue;
    const checks = [];
    if (field.required) checks.push(rules.required());
    if (field.maxLength && field.type !== 'list' && field.type !== 'tags') checks.push(rules.maxLength(field.maxLength));
    if (field.type === 'email') checks.push(rules.email());
    if (field.type === 'url') checks.push(rules.url());
    if (field.type === 'number') checks.push(v => (v != null && v !== '' && ((field.min != null && v < field.min) || (field.max != null && v > field.max)) ? `Enter a value between ${field.min} and ${field.max}.` : null));
    if (field.validate) checks.push(field.validate);
    schema[field.name] = checks;
  }

  return {
    form,
    getValues,
    /** Validates visible fields; shows errors and focuses the first one. Returns true when valid. */
    validate() {
      const values = getValues();
      const visibleSchema = Object.fromEntries(Object.entries(schema).filter(([name]) => !wrappers.get(name)?.hidden));
      const errors = validate(values, visibleSchema);
      if (Object.keys(errors).length) { showErrors(form, errors)?.focus(); return false; }
      clearErrors(form);
      return true;
    },
    showServerErrors(err) {
      if (err?.fieldErrors && Object.keys(err.fieldErrors).length) showErrors(form, err.fieldErrors)?.focus();
    },
  };
}

/** Button with an inline spinner while an async action runs. */
export async function withBusy(button, task, busyLabel = 'Saving…') {
  const original = [...button.childNodes];
  button.disabled = true;
  button.replaceChildren(h('span', { class: 'spinner-border spinner-border-sm me-1', 'aria-hidden': 'true' }), busyLabel);
  try { return await task(); } finally { button.disabled = false; button.replaceChildren(...original); }
}

/** Inline alert shown inside forms and modals. */
export function alertBox(message, variant = 'danger') {
  return h('div', { class: `alert alert-${variant} d-flex gap-2 align-items-start`, role: variant === 'danger' ? 'alert' : 'status' },
    icon(variant === 'danger' ? 'exclamation-octagon-fill' : 'info-circle-fill', 'mt-1'), h('div', null, message));
}

function mountModal(content, { size = '', labelId }) {
  const modal = h('div', { class: 'modal fade', tabindex: '-1', 'aria-labelledby': labelId, 'aria-modal': 'true', role: 'dialog' },
    h('div', { class: `modal-dialog modal-dialog-scrollable ${size}` }, h('div', { class: 'modal-content' }, content)));
  document.body.append(modal);
  const instance = new window.bootstrap.Modal(modal);
  modal.addEventListener('hidden.bs.modal', () => { instance.dispose(); modal.remove(); });
  return { modal, instance };
}

/**
 * Open a modal with a generated form. Resolves with onSubmit's result, or null if cancelled.
 * onSubmit should throw an ApiError to keep the modal open and show errors.
 */
export function openFormModal({ title, fields, values = {}, submitLabel = 'Save', size = 'modal-lg', intro, onSubmit }) {
  return new Promise(resolve => {
    const labelId = `modal-title-${++uid}`;
    const built = buildForm(fields, values);
    const errorSlot = h('div');
    const submit = h('button', { type: 'submit', class: 'btn btn-primary' }, submitLabel);
    built.form.id = `modal-form-${uid}`;
    submit.setAttribute('form', built.form.id);
    let result = null;

    const { modal, instance } = mountModal([
      h('div', { class: 'modal-header' }, h('h2', { class: 'modal-title h5', id: labelId }, title),
        h('button', { type: 'button', class: 'btn-close', 'data-bs-dismiss': 'modal', 'aria-label': 'Close' })),
      h('div', { class: 'modal-body' }, intro && h('p', { class: 'text-body-secondary small' }, intro), errorSlot, built.form),
      h('div', { class: 'modal-footer' }, h('button', { type: 'button', class: 'btn btn-light', 'data-bs-dismiss': 'modal' }, 'Cancel'), submit),
    ], { size, labelId });

    built.form.addEventListener('submit', async e => {
      e.preventDefault();
      errorSlot.replaceChildren();
      if (!built.validate()) return;
      try {
        result = await withBusy(submit, () => onSubmit(built.getValues()));
        instance.hide();
      } catch (err) {
        built.showServerErrors(err);
        errorSlot.replaceChildren(alertBox(err.message));
      }
    });
    modal.addEventListener('shown.bs.modal', () => built.form.querySelector('input:not([type=checkbox]), textarea, select')?.focus());
    modal.addEventListener('hidden.bs.modal', () => resolve(result));
    instance.show();
  });
}

/** Confirmation dialog for destructive actions. Resolves true when confirmed. */
export function confirmDialog({ title = 'Are you sure?', message, confirmLabel = 'Delete', variant = 'danger' }) {
  return new Promise(resolve => {
    const labelId = `confirm-title-${++uid}`;
    let confirmed = false;
    const confirmBtn = h('button', { type: 'button', class: `btn btn-${variant}` }, confirmLabel);
    const { modal, instance } = mountModal([
      h('div', { class: 'modal-header' }, h('h2', { class: 'modal-title h5', id: labelId }, title),
        h('button', { type: 'button', class: 'btn-close', 'data-bs-dismiss': 'modal', 'aria-label': 'Close' })),
      h('div', { class: 'modal-body' }, h('p', { class: 'mb-0' }, message)),
      h('div', { class: 'modal-footer' }, h('button', { type: 'button', class: 'btn btn-light', 'data-bs-dismiss': 'modal' }, 'Cancel'), confirmBtn),
    ], { size: '', labelId });
    confirmBtn.addEventListener('click', () => { confirmed = true; instance.hide(); });
    modal.addEventListener('shown.bs.modal', () => confirmBtn.focus());
    modal.addEventListener('hidden.bs.modal', () => resolve(confirmed));
    instance.show();
  });
}

export function pageHeader(title, description, ...actions) {
  return h('div', { class: 'd-flex flex-wrap justify-content-between align-items-start gap-3 mb-4' },
    h('div', null, h('h1', { class: 'h3 mb-1' }, title), description && h('p', { class: 'text-body-secondary mb-0' }, description)),
    actions.length > 0 && h('div', { class: 'd-flex flex-wrap gap-2' }, actions));
}

export function emptyState(iconName, text, action) {
  return h('div', { class: 'empty-state' }, icon(iconName), h('p', { class: 'mb-3' }, text), action);
}

export function loading(label = 'Loading…') {
  return h('div', { class: 'text-center py-5', role: 'status' },
    h('div', { class: 'spinner-border text-primary', 'aria-hidden': 'true' }), h('p', { class: 'mt-2 text-body-secondary small' }, label));
}

export function errorState(message, retry) {
  return h('div', { class: 'empty-state' }, icon('exclamation-triangle', 'text-danger'), h('p', null, message),
    retry && h('button', { type: 'button', class: 'btn btn-outline-primary', onclick: retry }, icon('arrow-clockwise'), ' Retry'));
}

export function publishedBadge(isPublished) {
  return isPublished
    ? h('span', { class: 'badge rounded-pill text-bg-success' }, 'Published')
    : h('span', { class: 'badge rounded-pill text-bg-secondary' }, 'Draft');
}

export function iconButton(iconName, label, onclick, variant = 'btn-light') {
  return h('button', { type: 'button', class: `btn btn-sm ${variant}`, 'aria-label': label, title: label, onclick }, icon(iconName));
}

/** Move an item within an array and return a new array. */
export function moveItem(list, index, delta) {
  const next = [...list];
  const target = index + delta;
  if (target < 0 || target >= next.length) return next;
  [next[index], next[target]] = [next[target], next[index]];
  return next;
}
