// Small client-side validation layer. The server validates everything again; this is for fast, accessible feedback.

const EMAIL = /^[^\s@]+@[^\s@]+\.[^\s@]{2,}$/;
const PHONE = /^\+?[0-9 ()-]{6,30}$/;

export const rules = {
  required: (msg = 'This field is required.') => v => (v == null || String(v).trim() === '' || (Array.isArray(v) && !v.length) ? msg : null),
  minLength: (n, msg) => v => (v && String(v).trim().length < n ? msg ?? `Use at least ${n} characters.` : null),
  maxLength: (n, msg) => v => (v && String(v).length > n ? msg ?? `Use at most ${n} characters.` : null),
  email: (msg = 'Enter a valid email address.') => v => (v && !EMAIL.test(String(v).trim()) ? msg : null),
  phone: (msg = 'Enter a valid phone number.') => v => (v && !PHONE.test(String(v).trim()) ? msg : null),
  url: (msg = 'Enter a full URL starting with http:// or https://.') => v => {
    if (!v) return null;
    try { const u = new URL(String(v).trim()); return ['http:', 'https:'].includes(u.protocol) ? null : msg; } catch { return msg; }
  },
  pattern: (re, msg) => v => (v && !re.test(String(v)) ? msg : null),
};

/**
 * @param {Record<string, any>} values
 * @param {Record<string, Array<(v:any, all:Record<string, any>) => string|null>>} schema
 * @returns {Record<string, string>} errors keyed by field
 */
export function validate(values, schema) {
  const errors = {};
  for (const [field, checks] of Object.entries(schema)) {
    for (const check of checks) {
      const msg = check(values[field], values);
      if (msg) { errors[field] = msg; break; }
    }
  }
  return errors;
}

/**
 * Show errors on a form's controls using Bootstrap's is-invalid + invalid-feedback, wired with aria attributes.
 * Controls are matched by name. Returns the first invalid control (focus it).
 * @param {HTMLFormElement} form
 * @param {Record<string, string | string[]>} errors
 */
export function showErrors(form, errors) {
  clearErrors(form);
  let first = null;
  for (const [name, value] of Object.entries(errors)) {
    const message = Array.isArray(value) ? value[0] : value;
    const control = form.elements.namedItem(name);
    if (!(control instanceof HTMLElement)) continue;
    control.classList.add('is-invalid');
    control.setAttribute('aria-invalid', 'true');
    const feedback = document.createElement('div');
    feedback.className = 'invalid-feedback';
    feedback.id = `${control.id || name}-error`;
    feedback.textContent = message;
    const anchor = control.closest('.input-group, .form-check, .form-switch') ?? control;
    anchor.after(feedback);
    if (anchor !== control) feedback.classList.add('d-block');
    control.setAttribute('aria-describedby', [control.getAttribute('data-help-id'), feedback.id].filter(Boolean).join(' '));
    first ??= control;
  }
  return first;
}

export function clearErrors(form) {
  form.querySelectorAll('.is-invalid').forEach(el => {
    el.classList.remove('is-invalid');
    el.removeAttribute('aria-invalid');
    const help = el.getAttribute('data-help-id');
    if (help) el.setAttribute('aria-describedby', help); else el.removeAttribute('aria-describedby');
  });
  form.querySelectorAll('.invalid-feedback').forEach(el => el.remove());
}
