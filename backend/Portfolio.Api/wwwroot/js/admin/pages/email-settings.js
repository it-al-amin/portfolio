// /admin/settings/email — delivery settings, behaviour switches, auto-reply template, test email, and public contact options.
import { h, icon } from '../../lib/dom.js';
import { api } from '../../lib/api.js';
import { toast } from '../../lib/toast.js';
import { buildForm, withBusy, alertBox, pageHeader } from '../ui.js';
import { profileFormPanel, contactFields } from './profile.js';

const fields = [
  { type: 'section', name: 'r', label: 'Recipients & sender' },
  { name: 'recipientEmail', label: 'Recipient for portfolio inquiries', type: 'email', maxLength: 254, col: 'col-md-6', help: 'Where new contact form messages are sent.' },
  { name: 'subjectPrefix', label: 'Subject prefix', maxLength: 40, col: 'col-md-6', placeholder: '[Portfolio]' },
  { name: 'fromEmail', label: 'From address', type: 'email', maxLength: 254, col: 'col-md-6', help: 'Must be a sender verified with your email provider.' },
  { name: 'senderName', label: 'Sender display name', maxLength: 100, col: 'col-md-6' },
  { type: 'section', name: 's', label: 'SMTP delivery' },
  { name: 'smtpHost', label: 'SMTP host', maxLength: 200, col: 'col-md-6', placeholder: 'smtp-relay.brevo.com' },
  { name: 'smtpPort', label: 'Port', type: 'number', min: 1, max: 65535, col: 'col-md-2', placeholder: '587' },
  { name: 'smtpSecurity', label: 'Encryption', type: 'select', col: 'col-md-4', options: [
    { value: 'Auto', label: 'Automatic' }, { value: 'StartTls', label: 'STARTTLS (port 587)' },
    { value: 'SslOnConnect', label: 'SSL/TLS (port 465)' }, { value: 'None', label: 'None (not recommended)' }] },
  { name: 'smtpUsername', label: 'SMTP username', maxLength: 254, col: 'col-md-6' },
  { type: 'section', name: 'b', label: 'Behaviour' },
  { name: 'saveToInbox', label: 'Save messages to the admin inbox', type: 'checkbox', help: 'Recommended. Messages are kept even if email delivery fails.' },
  { name: 'notifyOnNewMessage', label: 'Email me when a new message arrives', type: 'checkbox' },
  { name: 'sendVisitorConfirmation', label: 'Send visitors a confirmation / auto-reply email', type: 'checkbox' },
  { name: 'autoReplySubject', label: 'Auto-reply subject', maxLength: 150, showIf: v => v.sendVisitorConfirmation, placeholder: 'Thanks for getting in touch, {name}' },
  { name: 'autoReplyBody', label: 'Auto-reply message', type: 'textarea', rows: 6, maxLength: 3000, showIf: v => v.sendVisitorConfirmation,
    help: 'Leave empty for the default confirmation. Placeholders: {name}, {subject}. A copy of the visitor\'s message is included.' },
];

function statusCard(view) {
  const row = (label, value, ok) => h('li', { class: 'd-flex justify-content-between gap-3 py-2 border-bottom' },
    h('span', { class: 'text-body-secondary' }, label),
    h('span', { class: 'text-end fw-medium text-break' }, ok === undefined ? null : icon(ok ? 'check-circle-fill' : 'x-circle-fill', ok ? 'text-success me-1' : 'text-danger me-1'), value || 'Not set'));
  return h('section', { class: 'panel mb-4', 'aria-labelledby': 'email-status-title' },
    h('div', { class: 'panel-header d-flex justify-content-between align-items-center' },
      h('h2', { class: 'h6 mb-0', id: 'email-status-title' }, 'Delivery status'),
      view.isConfigured ? h('span', { class: 'badge text-bg-success' }, 'Configured') : h('span', { class: 'badge text-bg-warning' }, 'Not configured')),
    h('div', { class: 'panel-body' },
      !view.isConfigured && alertBox('Email delivery is not configured yet. Contact form messages are still saved to the inbox (if enabled), but no emails are sent until the SMTP host, from address and recipient are set.', 'warning'),
      h('ul', { class: 'list-unstyled small mb-3' },
        row('SMTP server', view.effectiveHost ? `${view.effectiveHost}:${view.effectivePort}` : '', Boolean(view.effectiveHost)),
        row('Username', view.effectiveUsername),
        row('Password', view.passwordConfigured ? '•••••••• (set on server)' : 'Not set on server', view.passwordConfigured),
        row('From address', view.effectiveFromEmail, Boolean(view.effectiveFromEmail)),
        row('Recipient', view.effectiveRecipient, Boolean(view.effectiveRecipient))),
      h('p', { class: 'small text-body-secondary mb-0' }, icon('shield-lock'), ' For security, the SMTP password is never stored in the database or shown here. Set it on the server with the ',
        h('code', null, 'SMTP_PASSWORD'), ' environment variable or ', h('code', null, 'dotnet user-secrets'), '. Empty fields below fall back to server configuration.')));
}

function testPanel() {
  const input = h('input', { type: 'email', class: 'form-control', id: 'test-to', placeholder: 'Defaults to the recipient address', maxlength: 254 });
  const send = h('button', { type: 'submit', class: 'btn btn-outline-primary' }, icon('send'), ' Send test email');
  const result = h('div', { class: 'mt-3', 'aria-live': 'polite' });
  const form = h('form', { novalidate: true },
    h('label', { class: 'form-label fw-medium', for: 'test-to' }, 'Send a test to'),
    h('div', { class: 'input-group' }, input, send), result);
  form.addEventListener('submit', async e => {
    e.preventDefault();
    result.replaceChildren();
    try {
      const res = await withBusy(send, () => api('/api/admin/settings/email/test', { method: 'POST', body: { to: input.value.trim() || null } }), 'Sending…');
      result.replaceChildren(alertBox(res.message, 'success'));
    } catch (err) {
      const detail = err.status === 502 ? ' Check the host, port, encryption, username and server password.' : '';
      result.replaceChildren(alertBox(err.message + detail));
    }
  });
  return h('section', { class: 'panel mb-4' },
    h('div', { class: 'panel-header' }, h('h2', { class: 'h6 mb-0' }, 'Test delivery')),
    h('div', { class: 'panel-body' }, h('p', { class: 'small text-body-secondary' }, 'Save your settings first, then send a test message.'), form));
}

export async function emailSettingsPage() {
  const [view, profile] = await Promise.all([api('/api/admin/settings/email'), api('/api/admin/profile')]);
  const statusSlot = h('div', null, statusCard(view));
  const built = buildForm(fields, view.settings);
  const errorSlot = h('div');
  const save = h('button', { type: 'submit', class: 'btn btn-primary' }, icon('check-lg'), ' Save email settings');
  built.form.append(h('div', { class: 'd-flex justify-content-end mt-4' }, save));
  built.form.addEventListener('submit', async e => {
    e.preventDefault();
    errorSlot.replaceChildren();
    if (!built.validate()) return;
    try {
      const updated = await withBusy(save, () => api('/api/admin/settings/email', { method: 'PUT', body: built.getValues() }));
      statusSlot.replaceChildren(statusCard(updated));
      toast('Email settings saved.');
    } catch (err) { built.showServerErrors(err); errorSlot.replaceChildren(alertBox(err.message)); }
  });

  return h('div', null,
    pageHeader('Email & contact settings', 'How contact form messages are delivered, and which contact options visitors see.'),
    h('div', { class: 'row g-4' },
      h('div', { class: 'col-xl-8' },
        h('section', { class: 'panel mb-4' }, h('div', { class: 'panel-body' }, errorSlot, built.form)),
        profileFormPanel(profile, contactFields, { title: 'Public contact options (shown on the portfolio)' })),
      h('div', { class: 'col-xl-4' }, statusSlot, testPanel())));
}
