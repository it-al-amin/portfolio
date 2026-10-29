// Admin sign-in page.
import { h, icon } from '../../lib/dom.js';
import { api } from '../../lib/api.js';
import { rules, validate, showErrors, clearErrors } from '../../lib/validate.js';
import { alertBox, withBusy } from '../ui.js';

export function loginPage({ onSuccess }) {
  const email = h('input', { type: 'email', class: 'form-control', id: 'login-email', name: 'email', autocomplete: 'username', required: true, maxlength: 254 });
  const password = h('input', { type: 'password', class: 'form-control', id: 'login-password', name: 'password', autocomplete: 'current-password', required: true, maxlength: 200 });
  const toggle = h('button', { type: 'button', class: 'btn btn-outline-secondary', 'aria-label': 'Show password', 'aria-pressed': 'false' }, icon('eye'));
  toggle.addEventListener('click', () => {
    const show = password.type === 'password';
    password.type = show ? 'text' : 'password';
    toggle.setAttribute('aria-pressed', String(show));
    toggle.setAttribute('aria-label', show ? 'Hide password' : 'Show password');
    toggle.replaceChildren(icon(show ? 'eye-slash' : 'eye'));
  });
  const errorSlot = h('div', { 'aria-live': 'assertive' });
  const submit = h('button', { type: 'submit', class: 'btn btn-primary btn-lg w-100' }, 'Sign in');

  const form = h('form', { novalidate: true },
    errorSlot,
    h('div', { class: 'mb-3' }, h('label', { class: 'form-label fw-medium', for: 'login-email' }, 'Email'), email),
    h('div', { class: 'mb-4' }, h('label', { class: 'form-label fw-medium', for: 'login-password' }, 'Password'),
      h('div', { class: 'input-group' }, password, toggle)),
    submit);

  form.addEventListener('submit', async e => {
    e.preventDefault();
    errorSlot.replaceChildren();
    const values = { email: email.value.trim(), password: password.value };
    const errors = validate(values, { email: [rules.required('Enter your email.'), rules.email()], password: [rules.required('Enter your password.')] });
    if (Object.keys(errors).length) { showErrors(form, errors)?.focus(); return; }
    clearErrors(form);
    try {
      const session = await withBusy(submit, () => api('/api/auth/login', { method: 'POST', body: values, skipAuthRedirect: true }), 'Signing in…');
      onSuccess(session);
    } catch (err) {
      errorSlot.replaceChildren(alertBox(err.message));
      password.select();
    }
  });

  setTimeout(() => email.focus(), 0);
  return h('main', { class: 'login-wrap', id: 'admin-main' },
    h('div', { class: 'login-card' },
      h('div', { class: 'text-center mb-4' },
        h('span', { class: 'icon-tile mb-3', 'aria-hidden': 'true' }, icon('shield-lock')),
        h('h1', { class: 'h3 mb-1' }, 'Admin sign in'),
        h('p', { class: 'text-body-secondary' }, 'Manage your portfolio content.')),
      h('div', { class: 'panel p-4' }, form),
      h('p', { class: 'text-center mt-4 small' }, h('a', { href: '/', class: 'link-secondary' }, icon('arrow-left'), ' Back to portfolio'))));
}
