// Generic list page for orderable content: search, reorder, publish toggle, create/edit (modal or page) and delete.
import { h, icon } from '../lib/dom.js';
import { api } from '../lib/api.js';
import { toast } from '../lib/toast.js';
import { openFormModal, confirmDialog, pageHeader, emptyState, loading, errorState, publishedBadge, iconButton, moveItem } from './ui.js';

/**
 * @typedef {{
 *   title: string, description?: string, endpoint: string, singular: string, icon: string,
 *   fields?: import('./ui.js').FieldDef[], defaults?: Record<string, any>,
 *   itemTitle: (item: any) => string, itemMeta?: (item: any) => any, searchText?: (item: any) => string,
 *   toInput?: (item: any) => Record<string, any>,
 *   editHref?: (item: any) => string, createHref?: string, extraActions?: (item: any, reload: () => void) => Node[],
 * }} ResourceConfig
 */

/** @param {ResourceConfig} config @param {{ navigate: (path: string) => void }} ctx */
export function resourcePage(config, ctx) {
  const base = `/api/admin/${config.endpoint}`;
  const listEl = h('div', { class: 'panel' }, loading());
  let items = [];
  let term = '';

  const toInput = config.toInput ?? (item => item);

  async function edit(item) {
    if (config.editHref) return ctx.navigate(config.editHref(item));
    const saved = await openFormModal({
      title: item ? `Edit ${config.singular}` : `Add ${config.singular}`,
      fields: config.fields,
      values: item ? toInput(item) : { ...config.defaults },
      onSubmit: values => api(item ? `${base}/${item.id}` : base, { method: item ? 'PUT' : 'POST', body: values }),
    });
    if (saved) { toast(`${config.singular[0].toUpperCase() + config.singular.slice(1)} saved.`); await load(); }
  }

  async function remove(item) {
    const ok = await confirmDialog({ title: `Delete ${config.singular}?`, message: `"${config.itemTitle(item)}" will be permanently deleted. This cannot be undone.` });
    if (!ok) return;
    try {
      await api(`${base}/${item.id}`, { method: 'DELETE' });
      toast(`${config.singular[0].toUpperCase() + config.singular.slice(1)} deleted.`);
      await load();
    } catch (err) { toast(err.message, 'danger'); }
  }

  async function togglePublished(item, checked, input) {
    try {
      await api(`${base}/${item.id}/publish`, { method: 'PATCH', body: { isPublished: checked } });
      item.isPublished = checked;
      toast(checked ? 'Published — now visible on the site.' : 'Unpublished — hidden from the site.');
      render();
    } catch (err) { input.checked = !checked; toast(err.message, 'danger'); }
  }

  async function move(index, delta) {
    const previous = items;
    items = moveItem(items, index, delta);
    render();
    try {
      await api(`${base}/reorder`, { method: 'PUT', body: { ids: items.map(i => i.id) } });
    } catch (err) {
      items = previous;
      render();
      toast(err.message, 'danger');
    }
  }

  function render() {
    const q = term.trim().toLowerCase();
    const searchText = config.searchText ?? config.itemTitle;
    const visible = q ? items.filter(i => searchText(i).toLowerCase().includes(q)) : items;

    if (!items.length) {
      listEl.replaceChildren(emptyState(config.icon, `No ${config.title.toLowerCase()} yet.`,
        h('button', { type: 'button', class: 'btn btn-primary', onclick: () => config.createHref ? ctx.navigate(config.createHref) : edit(null) }, icon('plus-lg'), ` Add ${config.singular}`)));
      return;
    }
    if (!visible.length) {
      listEl.replaceChildren(emptyState('search', 'Nothing matches your search.'));
      return;
    }

    listEl.replaceChildren(...visible.map(item => {
      const index = items.indexOf(item);
      const title = config.itemTitle(item);
      const switchId = `pub-${config.endpoint}-${item.id}`;
      const toggle = h('input', { class: 'form-check-input', type: 'checkbox', role: 'switch', id: switchId });
      toggle.checked = item.isPublished;
      toggle.addEventListener('change', () => togglePublished(item, toggle.checked, toggle));

      return h('div', { class: 'item-row' },
        h('div', { class: 'order-buttons' },
          h('button', { type: 'button', class: 'btn btn-sm btn-link text-body-secondary', 'aria-label': `Move "${title}" up`, disabled: Boolean(q) || index === 0, onclick: () => move(index, -1) }, icon('chevron-up')),
          h('button', { type: 'button', class: 'btn btn-sm btn-link text-body-secondary', 'aria-label': `Move "${title}" down`, disabled: Boolean(q) || index === items.length - 1, onclick: () => move(index, 1) }, icon('chevron-down'))),
        h('div', { class: 'item-main' },
          h('div', { class: 'd-flex flex-wrap align-items-center gap-2' }, h('span', { class: 'item-title' }, title), publishedBadge(item.isPublished)),
          config.itemMeta && h('div', { class: 'item-meta' }, config.itemMeta(item))),
        h('div', { class: 'item-actions align-items-center' },
          h('div', { class: 'form-check form-switch mb-0 me-2', title: 'Published' },
            toggle, h('label', { class: 'form-check-label small', for: switchId }, h('span', { class: 'visually-hidden' }, `Publish "${title}"`))),
          config.extraActions?.(item, load),
          iconButton('pencil', `Edit "${title}"`, () => edit(item)),
          iconButton('trash', `Delete "${title}"`, () => remove(item), 'btn-light text-danger')));
    }));
  }

  async function load() {
    try {
      items = await api(base);
      render();
    } catch (err) {
      listEl.replaceChildren(errorState(err.message, load));
    }
  }

  load();

  return h('div', null,
    pageHeader(config.title, config.description,
      h('button', { type: 'button', class: 'btn btn-primary', onclick: () => config.createHref ? ctx.navigate(config.createHref) : edit(null) }, icon('plus-lg'), ` Add ${config.singular}`)),
    h('div', { class: 'input-group mb-3 project-search' },
      h('span', { class: 'input-group-text' }, icon('search')),
      h('input', { type: 'search', class: 'form-control', placeholder: `Search ${config.title.toLowerCase()}`, 'aria-label': `Search ${config.title.toLowerCase()}`,
        oninput: e => { term = e.target.value; render(); } })),
    h('p', { class: 'small text-body-secondary' }, icon('info-circle'), ' Use the arrows to change the display order on your portfolio. Reordering is disabled while searching.'),
    listEl);
}
