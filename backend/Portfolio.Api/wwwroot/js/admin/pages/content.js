// Field configurations for the simple content collections; all use the generic resource page.
import { resourcePage } from '../resource-page.js';
import { api } from '../../lib/api.js';
import { monthYear, date } from '../../lib/format.js';

export const experiencePage = ctx => resourcePage({
  title: 'Experience', singular: 'experience', endpoint: 'experiences', icon: 'briefcase',
  description: 'Work history shown in the Experience timeline.',
  defaults: { isPublished: true, isCurrent: false, responsibilities: [], technologies: [] },
  itemTitle: e => `${e.designation} · ${e.company}`,
  itemMeta: e => `${monthYear(e.startDate)} – ${e.isCurrent ? 'Present' : monthYear(e.endDate)}${e.employmentType ? ' · ' + e.employmentType : ''}`,
  searchText: e => [e.company, e.designation, ...e.technologies].join(' '),
  fields: [
    { name: 'designation', label: 'Designation / job title', required: true, maxLength: 120, col: 'col-md-6' },
    { name: 'company', label: 'Company', required: true, maxLength: 120, col: 'col-md-6' },
    { name: 'employmentType', label: 'Employment type', maxLength: 60, col: 'col-md-6', placeholder: 'Full-time · Remote' },
    { name: 'location', label: 'Location', maxLength: 120, col: 'col-md-6' },
    { name: 'startDate', label: 'Start month', type: 'month', required: true, col: 'col-md-4' },
    { name: 'endDate', label: 'End month', type: 'month', col: 'col-md-4', showIf: v => !v.isCurrent,
      validate: (v, all) => (!all.isCurrent && !v ? 'End month is required unless this is your current role.' : !all.isCurrent && all.startDate && v < all.startDate ? 'End must be after start.' : null) },
    { name: 'isCurrent', label: 'I currently work here', type: 'checkbox', col: 'col-md-4 d-flex align-items-end pb-2' },
    { name: 'summary', label: 'Short summary', type: 'textarea', rows: 2, maxLength: 1000 },
    { name: 'responsibilities', label: 'Responsibilities & contributions', type: 'list', rows: 6, help: 'One per line.' },
    { name: 'technologies', label: 'Technologies', type: 'tags', help: 'Comma separated, e.g. C#, ASP.NET Core, React' },
    { name: 'isPublished', label: 'Published (visible on the portfolio)', type: 'checkbox' },
  ],
}, ctx);

export const educationPage = ctx => resourcePage({
  title: 'Education', singular: 'education entry', endpoint: 'education', icon: 'mortarboard',
  description: 'Degrees and qualifications.',
  defaults: { isPublished: true },
  itemTitle: e => `${e.degree}${e.fieldOfStudy ? ' in ' + e.fieldOfStudy : ''}`,
  itemMeta: e => [e.institution, e.endYear, e.grade].filter(Boolean).join(' · '),
  fields: [
    { name: 'degree', label: 'Degree', required: true, maxLength: 150, col: 'col-md-6' },
    { name: 'fieldOfStudy', label: 'Field of study', maxLength: 150, col: 'col-md-6' },
    { name: 'institution', label: 'Institution', required: true, maxLength: 200 },
    { name: 'startYear', label: 'Start year', type: 'number', min: 1950, max: 2100, col: 'col-md-4' },
    { name: 'endYear', label: 'End / graduation year', type: 'number', min: 1950, max: 2100, col: 'col-md-4' },
    { name: 'grade', label: 'Grade / CGPA', maxLength: 60, col: 'col-md-4', placeholder: 'CGPA 3.53 / 4.00' },
    { name: 'description', label: 'Description', type: 'textarea', maxLength: 2000 },
    { name: 'isPublished', label: 'Published', type: 'checkbox' },
  ],
}, ctx);

export const servicesPage = ctx => resourcePage({
  title: 'Services', singular: 'service', endpoint: 'services', icon: 'grid',
  description: 'Services listed in the "How I can help" section.',
  defaults: { isPublished: true },
  itemTitle: s => s.title,
  itemMeta: s => s.description,
  fields: [
    { name: 'title', label: 'Title', required: true, maxLength: 120 },
    { name: 'description', label: 'Description', type: 'textarea', required: true, maxLength: 600 },
    { name: 'icon', label: 'Icon name', maxLength: 40, help: 'A Bootstrap Icons name, e.g. database, hdd-network, window-stack, plug.' },
    { name: 'isPublished', label: 'Published', type: 'checkbox' },
  ],
}, ctx);

/** Achievements & certifications; the document dropdown is loaded from the documents library. */
export async function certificationsPage(ctx) {
  let documents = [];
  try { documents = await api('/api/admin/documents'); } catch { /* the form still works without document links */ }
  const docOptions = [{ value: '', label: '— None —' }, ...documents.map(d => ({ value: d.id, label: `${d.title}${d.isPublic ? '' : ' (private — link hidden)'}` }))];

  return resourcePage({
    title: 'Achievements & certifications', singular: 'achievement', endpoint: 'achievements', icon: 'trophy',
    description: 'Awards, contests and certifications. Attach a certificate from Documents; it is only downloadable when the document is public.',
    defaults: { isPublished: true, kind: 'Achievement' },
    itemTitle: a => a.title,
    itemMeta: a => [a.kind, a.issuer, a.dateLabel || (a.date && date(a.date))].filter(Boolean).join(' · '),
    toInput: a => ({ ...a, documentId: a.documentId ?? '' }),
    fields: [
      { name: 'title', label: 'Title', required: true, maxLength: 200 },
      { name: 'kind', label: 'Type', type: 'select', col: 'col-md-6', options: [{ value: 'Achievement', label: 'Achievement' }, { value: 'Certification', label: 'Certification' }] },
      { name: 'issuer', label: 'Issuer / organisation', maxLength: 150, col: 'col-md-6' },
      { name: 'date', label: 'Date', type: 'date', col: 'col-md-6' },
      { name: 'dateLabel', label: 'Date label (instead of an exact date)', maxLength: 40, col: 'col-md-6', placeholder: '2018 – 2023' },
      { name: 'description', label: 'Description', type: 'textarea', maxLength: 2000 },
      { name: 'verificationUrl', label: 'Verification URL', type: 'url', maxLength: 500 },
      { name: 'documentId', label: 'Certificate document', type: 'select', options: docOptions },
      { name: 'isPublished', label: 'Published', type: 'checkbox' },
    ],
  }, ctx);
}
