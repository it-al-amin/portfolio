// Contact section: direct contact options plus a validated form that posts to /api/public/contact.
import { h, icon, externalLink, copyText } from '../lib/dom.js';
import { api } from '../lib/api.js';
import { rules, validate, showErrors, clearErrors } from '../lib/validate.js';
import { toast } from '../lib/toast.js';

const schema = {
  name: [rules.required('Please enter your name.'), rules.minLength(2), rules.maxLength(100)],
  email: [rules.required('Please enter your email address.'), rules.email(), rules.maxLength(254)],
  phone: [rules.phone(), rules.maxLength(30)],
  subject: [rules.required('Please add a subject.'), rules.minLength(3), rules.maxLength(150)],
  message: [rules.required('Please write a message.'), rules.minLength(10, 'Please write at least 10 characters.'), rules.maxLength(5000)],
};

function method({ href, iconName, label, value, external, download }) {
  const body = [
    h('span', { class: 'icon-tile' }, icon(iconName)),
    h('span', { class: 'd-flex flex-column min-w-0' }, h('span', { class: 'label' }, label), h('span', { class: 'value' }, value)),
  ];
  if (!href) return h('div', { class: 'contact-method' }, body);
  if (external) return externalLink(href, body, { class: 'contact-method' });
  return h('a', { class: 'contact-method', href, download: download ? '' : null }, body);
}

function field(id, label, control, { optional = false, help } = {}) {
  control.id = `contact-${id}`;
  control.name = id;
  if (help) control.setAttribute('data-help-id', `contact-${id}-help`), control.setAttribute('aria-describedby', `contact-${id}-help`);
  return h('div', { class: 'mb-3' },
    h('label', { class: 'form-label fw-medium', for: control.id }, label, optional ? h('span', { class: 'text-body-secondary fw-normal' }, ' (optional)') : null),
    control,
    help && h('div', { class: 'form-text', id: `contact-${id}-help` }, help));
}

export function renderContact(data) {
  const p = data.profile;

  const methods = [
    p.email && method({ href: `mailto:${p.email}`, iconName: 'envelope', label: 'Email me', value: p.email }),
    p.phone && method({ href: `tel:${p.phone.replace(/[^+\d]/g, '')}`, iconName: 'telephone', label: 'Phone', value: p.phone }),
    p.whatsAppNumber && method({ href: `https://wa.me/${p.whatsAppNumber}`, iconName: 'whatsapp', label: 'WhatsApp', value: 'Start a chat', external: true }),
    p.linkedInUrl && method({ href: p.linkedInUrl, iconName: 'linkedin', label: 'LinkedIn', value: 'Connect with me', external: true }),
    p.gitHubUrl && method({ href: p.gitHubUrl, iconName: 'github', label: 'GitHub', value: p.gitHubUrl.replace(/^https?:\/\/(www\.)?/, ''), external: true }),
    p.location && method({ iconName: 'geo-alt', label: 'Location', value: p.location }),
  ].filter(Boolean);

  const copyButton = p.email && h('button', {
    type: 'button', class: 'btn btn-outline-primary',
    onclick: async e => {
      const btn = e.currentTarget;
      try {
        await copyText(p.email);
        toast('Email address copied to clipboard.');
        btn.replaceChildren(icon('check2'), ' Copied');
        setTimeout(() => btn.replaceChildren(icon('clipboard'), ' Copy email'), 2000);
      } catch {
        toast('Could not copy automatically. Please copy the address manually.', 'warning');
      }
    },
  }, icon('clipboard'), ' Copy email');

  // ---- Form ----
  const status = h('div', { class: 'mt-3', role: 'status', 'aria-live': 'polite' });
  const messageInput = h('textarea', { class: 'form-control', rows: 6, maxlength: 5000, required: true });
  const counter = h('span', { class: 'form-text' }, '0 / 5000');
  messageInput.addEventListener('input', () => { counter.textContent = `${messageInput.value.length} / 5000`; });
  const submit = h('button', { type: 'submit', class: 'btn btn-primary btn-lg w-100' }, icon('send'), ' Send message');

  const form = h('form', { class: 'card-soft p-4 p-md-5', novalidate: true, 'aria-labelledby': 'contact-form-title' },
    h('h3', { class: 'h4 mb-1', id: 'contact-form-title' }, 'Send a message'),
    h('p', { class: 'text-body-secondary small mb-4' }, 'I usually reply within a couple of days.'),
    h('div', { class: 'row g-0 gx-md-3' },
      h('div', { class: 'col-md-6' }, field('name', 'Full name', h('input', { type: 'text', class: 'form-control', autocomplete: 'name', maxlength: 100, required: true }))),
      h('div', { class: 'col-md-6' }, field('email', 'Email address', h('input', { type: 'email', class: 'form-control', autocomplete: 'email', maxlength: 254, required: true })))),
    h('div', { class: 'row g-0 gx-md-3' },
      h('div', { class: 'col-md-6' }, field('subject', 'Subject', h('input', { type: 'text', class: 'form-control', maxlength: 150, required: true }))),
      h('div', { class: 'col-md-6' }, field('phone', 'Phone', h('input', { type: 'tel', class: 'form-control', autocomplete: 'tel', maxlength: 30 }), { optional: true }))),
    field('message', 'Message', messageInput),
    h('div', { class: 'd-flex justify-content-end mt-n2 mb-3' }, counter),
    // Honeypot: hidden from people and assistive tech.
    h('div', { class: 'hp-field', 'aria-hidden': 'true' },
      h('label', { for: 'contact-website' }, 'Website'),
      h('input', { type: 'text', id: 'contact-website', name: 'website', tabindex: '-1', autocomplete: 'off' })),
    submit,
    status);

  let sending = false;
  form.addEventListener('submit', async e => {
    e.preventDefault();
    if (sending) return;
    const values = Object.fromEntries(new FormData(form));
    status.replaceChildren();
    const errors = validate(values, schema);
    if (Object.keys(errors).length) {
      showErrors(form, errors)?.focus();
      return;
    }
    clearErrors(form);

    sending = true;
    submit.disabled = true;
    submit.replaceChildren(h('span', { class: 'spinner-border spinner-border-sm', 'aria-hidden': 'true' }), ' Sending…');
    try {
      const result = await api('/api/public/contact', { method: 'POST', body: { ...values, phone: values.phone || null } });
      form.reset();
      counter.textContent = '0 / 5000';
      status.replaceChildren(h('div', { class: 'alert alert-success d-flex gap-2 mb-0' }, icon('check-circle-fill'), result.message));
    } catch (err) {
      if (err.fieldErrors && Object.keys(err.fieldErrors).length) showErrors(form, err.fieldErrors)?.focus();
      const fallback = p.email ? [' You can also email me directly at ', h('a', { href: `mailto:${p.email}` }, p.email), '.'] : null;
      status.replaceChildren(h('div', { class: 'alert alert-danger d-flex gap-2 mb-0', role: 'alert' },
        icon('exclamation-octagon-fill'), h('span', null, err.message, fallback)));
    } finally {
      sending = false;
      submit.disabled = false;
      submit.replaceChildren(icon('send'), ' Send message');
    }
  });

  return h('div', { class: 'container' },
    h('div', { class: 'mb-5 reveal' },
      h('p', { class: 'section-eyebrow' }, 'Contact'),
      h('h2', { class: 'section-title', id: 'contact-title' }, "Let's build something together"),
      h('p', { class: 'section-lead mb-0' }, 'Have a role, project or question in mind? Reach out through any of the options below.')),
    h('div', { class: 'row g-4' },
      h('div', { class: 'col-lg-5 reveal' },
        h('div', { class: 'd-grid gap-3 mb-3' }, methods),
        h('div', { class: 'd-flex flex-wrap gap-2' },
          copyButton,
          data.cvUrl && h('a', { class: 'btn btn-outline-primary', href: data.cvUrl, download: '' }, icon('download'), ' Download CV'))),
      h('div', { class: 'col-lg-7 reveal' }, form)));
}
