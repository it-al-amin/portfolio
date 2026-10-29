import { h, icon } from './dom.js';

/**
 * Show a short, polite notification.
 * @param {string} message
 * @param {'success'|'danger'|'info'|'warning'} [variant]
 */
export function toast(message, variant = 'success') {
  let container = document.getElementById('toasts');
  if (!container) {
    container = h('div', { id: 'toasts', class: 'toast-container position-fixed bottom-0 end-0 p-3' });
    document.body.append(container);
  }
  const iconName = { success: 'check-circle-fill', danger: 'exclamation-octagon-fill', warning: 'exclamation-triangle-fill', info: 'info-circle-fill' }[variant];
  const el = h('div', {
    class: `toast align-items-center border-0 text-bg-${variant}`,
    role: variant === 'danger' ? 'alert' : 'status',
    'aria-live': variant === 'danger' ? 'assertive' : 'polite',
    'aria-atomic': 'true',
  },
    h('div', { class: 'd-flex' },
      h('div', { class: 'toast-body d-flex align-items-center gap-2' }, icon(iconName), message),
      h('button', { type: 'button', class: 'btn-close btn-close-white me-2 m-auto', 'data-bs-dismiss': 'toast', 'aria-label': 'Close' })));
  container.append(el);
  const instance = window.bootstrap.Toast.getOrCreateInstance(el, { delay: variant === 'danger' ? 7000 : 3500 });
  el.addEventListener('hidden.bs.toast', () => el.remove());
  instance.show();
}
