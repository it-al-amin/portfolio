// Minimal DOM helpers. All text goes through textContent / text nodes, so user content is never parsed as HTML.

/**
 * Create an element.
 * @param {string} tag
 * @param {Record<string, any> | null} [props] - class, text, dataset, on<Event> handlers, or attributes (true = boolean attr).
 * @param {...any} children - Nodes, strings, numbers, arrays; null/false are skipped.
 * @returns {HTMLElement}
 */
export function h(tag, props, ...children) {
  const el = document.createElement(tag);
  for (const [key, value] of Object.entries(props ?? {})) {
    if (value == null || value === false) continue;
    if (key === 'class') el.className = value;
    else if (key === 'text') el.textContent = value;
    else if (key === 'dataset') Object.assign(el.dataset, value);
    else if (key.startsWith('on') && typeof value === 'function') el.addEventListener(key.slice(2).toLowerCase(), value);
    else if (key === 'href' || key === 'src') {
      const safe = safeUrl(value);
      if (safe) el.setAttribute(key, safe);
    } else if (key === 'value' && 'value' in el) el.value = value;
    else if (value === true) el.setAttribute(key, '');
    else el.setAttribute(key, String(value));
  }
  append(el, children);
  return el;
}

/** Append children (flattened) to an element. */
export function append(el, children) {
  for (const child of [children].flat(Infinity)) {
    if (child == null || child === false) continue;
    el.append(child instanceof Node ? child : document.createTextNode(String(child)));
  }
  return el;
}

/** Bootstrap icon element (decorative). */
export const icon = (name, extra = '') => h('i', { class: `bi bi-${name}${extra ? ' ' + extra : ''}`, 'aria-hidden': 'true' });

/**
 * Allows relative paths, fragments and http(s)/mailto/tel URLs only — blocks javascript: and data: URLs.
 * @param {string} url
 * @returns {string | null}
 */
export function safeUrl(url) {
  if (typeof url !== 'string' || !url.trim()) return null;
  const value = url.trim();
  if (value.startsWith('/') || value.startsWith('#') || value.startsWith('?')) return value.startsWith('//') ? null : value;
  try {
    const parsed = new URL(value);
    return ['http:', 'https:', 'mailto:', 'tel:'].includes(parsed.protocol) ? value : null;
  } catch {
    return null;
  }
}

/** External link that opens in a new tab safely. */
export function externalLink(href, label, props = {}) {
  return h('a', { href, target: '_blank', rel: 'noopener noreferrer', ...props }, label);
}

/** Initials for avatar fallbacks, e.g. "Md Al-Amin" -> "MA". */
export function initials(name) {
  return (name || '?').split(/[\s-]+/).filter(Boolean).slice(0, 2).map(p => p[0].toUpperCase()).join('');
}

/** Copy text to the clipboard, with a fallback for non-secure contexts. */
export async function copyText(text) {
  if (navigator.clipboard && window.isSecureContext) {
    await navigator.clipboard.writeText(text);
    return;
  }
  const area = h('textarea', { class: 'visually-hidden', readonly: true });
  area.value = text;
  document.body.append(area);
  area.select();
  const ok = document.execCommand('copy');
  area.remove();
  if (!ok) throw new Error('Copy failed');
}

export const qs = (selector, root = document) => root.querySelector(selector);
export const qsa = (selector, root = document) => [...root.querySelectorAll(selector)];
