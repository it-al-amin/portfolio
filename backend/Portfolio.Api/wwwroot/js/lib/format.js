// Formatting helpers shared by the public site and admin dashboard.

const MONTHS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];

/** "2024-08-01" -> "Aug 2024" (date-only strings are parsed without timezone shifts). */
export function monthYear(dateOnly) {
  if (!dateOnly) return '';
  const [y, m] = dateOnly.split('-').map(Number);
  return `${MONTHS[m - 1]} ${y}`;
}

/** Human duration between two date-only strings (end defaults to today), e.g. "2 yrs 3 mos". */
export function duration(start, end) {
  if (!start) return '';
  const [sy, sm] = start.split('-').map(Number);
  const now = new Date();
  const [ey, em] = end ? end.split('-').map(Number) : [now.getFullYear(), now.getMonth() + 1];
  const months = Math.max(1, (ey - sy) * 12 + (em - sm) + (end ? 0 : 1));
  const years = Math.floor(months / 12);
  const rest = months % 12;
  const parts = [];
  if (years) parts.push(`${years} yr${years > 1 ? 's' : ''}`);
  if (rest) parts.push(`${rest} mo${rest > 1 ? 's' : ''}`);
  return parts.join(' ');
}

/** ISO timestamp -> localized date and time. */
export function dateTime(iso) {
  if (!iso) return '';
  const d = new Date(/[zZ]|[+-]\d\d:\d\d$/.test(iso) ? iso : iso + 'Z');
  return d.toLocaleString(undefined, { year: 'numeric', month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' });
}

export function date(iso) {
  if (!iso) return '';
  if (/^\d{4}-\d{2}-\d{2}$/.test(iso)) {
    const [y, m, d] = iso.split('-').map(Number);
    return `${d} ${MONTHS[m - 1]} ${y}`;
  }
  return new Date(iso.endsWith('Z') ? iso : iso + 'Z').toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' });
}

export function bytes(n) {
  if (!n && n !== 0) return '';
  if (n < 1024) return `${n} B`;
  if (n < 1024 * 1024) return `${(n / 1024).toFixed(0)} KB`;
  return `${(n / (1024 * 1024)).toFixed(1)} MB`;
}

/** Split text into paragraphs on blank lines. */
export const paragraphs = text => (text || '').split(/\n\s*\n/).map(p => p.trim()).filter(Boolean);
