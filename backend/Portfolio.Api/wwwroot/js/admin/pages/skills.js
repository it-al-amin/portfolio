// Skills: categories (cards) containing skills (rows). Both are orderable and publishable.
import { h, icon } from '../../lib/dom.js';
import { api } from '../../lib/api.js';
import { toast } from '../../lib/toast.js';
import { openFormModal, confirmDialog, pageHeader, emptyState, loading, errorState, publishedBadge, iconButton, moveItem } from '../ui.js';

const PROFICIENCY = [{ value: '', label: '— Not shown —' }, ...['Beginner', 'Intermediate', 'Advanced', 'Expert'].map(v => ({ value: v, label: v }))];

const categoryFields = [
  { name: 'name', label: 'Category name', required: true, maxLength: 80 },
  { name: 'icon', label: 'Icon name', maxLength: 40, help: 'Bootstrap Icons name, e.g. code-slash, server, window, database, tools.' },
  { name: 'isPublished', label: 'Published', type: 'checkbox' },
];

const skillFields = categories => [
  { name: 'name', label: 'Skill', required: true, maxLength: 60 },
  { name: 'categoryId', label: 'Category', type: 'select', options: categories.map(c => ({ value: String(c.id), label: c.name })) },
  { name: 'proficiency', label: 'Proficiency label (optional)', type: 'select', options: PROFICIENCY, help: 'Shown as a small text label. No percentage bars are displayed.' },
  { name: 'isPublished', label: 'Published', type: 'checkbox' },
];

export function skillsPage() {
  const container = h('div', null, loading());
  let categories = [];
  let skills = [];

  async function load() {
    try {
      [categories, skills] = await Promise.all([api('/api/admin/skill-categories'), api('/api/admin/skills')]);
      render();
    } catch (err) { container.replaceChildren(errorState(err.message, load)); }
  }

  async function editCategory(cat) {
    const saved = await openFormModal({
      title: cat ? 'Edit category' : 'Add category', size: '', fields: categoryFields, values: cat ?? { isPublished: true },
      onSubmit: v => api(cat ? `/api/admin/skill-categories/${cat.id}` : '/api/admin/skill-categories', { method: cat ? 'PUT' : 'POST', body: v }),
    });
    if (saved) { toast('Category saved.'); load(); }
  }

  async function editSkill(skill, categoryId) {
    const saved = await openFormModal({
      title: skill ? 'Edit skill' : 'Add skill', size: '', fields: skillFields(categories),
      values: skill ? { ...skill, categoryId: String(skill.categoryId), proficiency: skill.proficiency ?? '' } : { isPublished: true, categoryId: String(categoryId), proficiency: '' },
      onSubmit: v => api(skill ? `/api/admin/skills/${skill.id}` : '/api/admin/skills', { method: skill ? 'PUT' : 'POST', body: { ...v, categoryId: Number(v.categoryId) } }),
    });
    if (saved) { toast('Skill saved.'); load(); }
  }

  async function remove(kind, item) {
    const isCategory = kind === 'category';
    const ok = await confirmDialog({
      title: `Delete ${kind}?`,
      message: isCategory ? `"${item.name}" and all of its skills will be permanently deleted.` : `"${item.name}" will be permanently deleted.`,
    });
    if (!ok) return;
    try {
      await api(`/api/admin/${isCategory ? 'skill-categories' : 'skills'}/${item.id}`, { method: 'DELETE' });
      toast(`${isCategory ? 'Category' : 'Skill'} deleted.`); load();
    } catch (err) { toast(err.message, 'danger'); }
  }

  async function reorder(endpoint, ordered) {
    try { await api(`/api/admin/${endpoint}/reorder`, { method: 'PUT', body: { ids: ordered.map(x => x.id) } }); }
    catch (err) { toast(err.message, 'danger'); }
    load();
  }

  async function togglePublish(endpoint, item) {
    try {
      await api(`/api/admin/${endpoint}/${item.id}/publish`, { method: 'PATCH', body: { isPublished: !item.isPublished } });
      toast(item.isPublished ? 'Hidden from the portfolio.' : 'Visible on the portfolio.'); load();
    } catch (err) { toast(err.message, 'danger'); }
  }

  function render() {
    if (!categories.length) {
      container.replaceChildren(emptyState('tools', 'No skill categories yet.',
        h('button', { type: 'button', class: 'btn btn-primary', onclick: () => editCategory(null) }, icon('plus-lg'), ' Add category')));
      return;
    }
    container.replaceChildren(h('div', { class: 'row g-4' }, categories.map((cat, ci) => {
      const catSkills = skills.filter(s => s.categoryId === cat.id);
      return h('div', { class: 'col-lg-6' },
        h('section', { class: 'panel h-100', 'aria-label': cat.name },
          h('div', { class: 'panel-header d-flex flex-wrap align-items-center gap-2' },
            h('span', { class: 'icon-tile' }, icon(cat.icon || 'grid')),
            h('div', { class: 'flex-grow-1' }, h('h2', { class: 'h6 mb-0' }, cat.name), publishedBadge(cat.isPublished)),
            iconButton('chevron-up', `Move ${cat.name} up`, () => ci > 0 && reorder('skill-categories', moveItem(categories, ci, -1))),
            iconButton('chevron-down', `Move ${cat.name} down`, () => ci < categories.length - 1 && reorder('skill-categories', moveItem(categories, ci, 1))),
            iconButton(cat.isPublished ? 'eye-slash' : 'eye', cat.isPublished ? `Hide ${cat.name}` : `Publish ${cat.name}`, () => togglePublish('skill-categories', cat)),
            iconButton('pencil', `Edit ${cat.name}`, () => editCategory(cat)),
            iconButton('trash', `Delete ${cat.name}`, () => remove('category', cat), 'btn-light text-danger')),
          h('div', null,
            catSkills.length ? catSkills.map((s, si) => h('div', { class: 'item-row py-2' },
              h('div', { class: 'item-main' },
                h('span', { class: `item-title ${s.isPublished ? '' : 'text-body-secondary text-decoration-line-through'}` }, s.name),
                s.proficiency && h('span', { class: 'item-meta ms-2' }, s.proficiency),
                !s.isPublished && h('span', { class: 'visually-hidden' }, ' (hidden)')),
              h('div', { class: 'item-actions' },
                iconButton('chevron-up', `Move ${s.name} up`, () => si > 0 && reorder('skills', moveItem(catSkills, si, -1))),
                iconButton('chevron-down', `Move ${s.name} down`, () => si < catSkills.length - 1 && reorder('skills', moveItem(catSkills, si, 1))),
                iconButton(s.isPublished ? 'eye-slash' : 'eye', s.isPublished ? `Hide ${s.name}` : `Publish ${s.name}`, () => togglePublish('skills', s)),
                iconButton('pencil', `Edit ${s.name}`, () => editSkill(s)),
                iconButton('trash', `Delete ${s.name}`, () => remove('skill', s), 'btn-light text-danger'))))
              : h('p', { class: 'text-body-secondary small p-3 mb-0' }, 'No skills in this category yet.'),
            h('div', { class: 'p-3' }, h('button', { type: 'button', class: 'btn btn-sm btn-outline-primary', onclick: () => editSkill(null, cat.id) }, icon('plus-lg'), ' Add skill')))));
    })));
  }

  load();
  return h('div', null,
    pageHeader('Skills', 'Group your technical skills into categories. Hidden items and empty categories are not shown publicly.',
      h('button', { type: 'button', class: 'btn btn-primary', onclick: () => editCategory(null) }, icon('plus-lg'), ' Add category')),
    container);
}
