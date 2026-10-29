// Fetch wrapper for the ASP.NET Core API. Sends the CSRF header the server requires on state-changing requests.

export class ApiError extends Error {
  /**
   * @param {number} status
   * @param {string} message
   * @param {Record<string, string[]>} [fieldErrors] - keyed by camelCase field name
   */
  constructor(status, message, fieldErrors = {}) {
    super(message);
    this.status = status;
    this.fieldErrors = fieldErrors;
  }
}

const GENERIC_VALIDATION_TITLE = 'One or more validation errors occurred.';

function defaultMessage(status) {
  if (status === 401) return 'Your session has expired. Please sign in again.';
  if (status === 403) return 'You do not have permission to do that.';
  if (status === 404) return 'The requested item was not found.';
  if (status === 413) return 'The file is too large.';
  if (status === 429) return 'Too many requests. Please wait a moment and try again.';
  if (status >= 500) return 'Something went wrong on the server. Please try again.';
  return 'The request could not be completed.';
}

const camel = key => {
  const clean = key.replace(/^\$\.?/, '');
  return clean ? clean[0].toLowerCase() + clean.slice(1) : '_';
};

let unauthorizedHandler = null;
/** Register a callback for 401 responses (used by the admin app to redirect to login). */
export function onUnauthorized(handler) { unauthorizedHandler = handler; }

/**
 * @param {string} path
 * @param {{ method?: string, body?: any, form?: FormData, signal?: AbortSignal, skipAuthRedirect?: boolean }} [options]
 */
export async function api(path, { method = 'GET', body, form, signal, skipAuthRedirect = false } = {}) {
  const headers = { 'X-Requested-With': 'fetch', Accept: 'application/json' };
  let payload;
  if (form) payload = form;
  else if (body !== undefined) {
    headers['Content-Type'] = 'application/json';
    payload = JSON.stringify(body);
  }

  let response;
  try {
    response = await fetch(path, { method, headers, body: payload, credentials: 'same-origin', signal });
  } catch (err) {
    if (err?.name === 'AbortError') throw err;
    throw new ApiError(0, 'Could not reach the server. Check your connection and try again.');
  }

  if (response.status === 204) return null;
  const isJson = (response.headers.get('content-type') || '').includes('json');
  const data = isJson ? await response.json().catch(() => null) : null;

  if (!response.ok) {
    if (response.status === 401 && unauthorizedHandler && !skipAuthRedirect) unauthorizedHandler();
    const fieldErrors = {};
    for (const [key, messages] of Object.entries(data?.errors ?? {})) fieldErrors[camel(key)] = messages;
    const firstFieldError = Object.values(fieldErrors)[0]?.[0];
    const title = data?.title && data.title !== GENERIC_VALIDATION_TITLE ? data.title : null;
    throw new ApiError(response.status, title ?? firstFieldError ?? defaultMessage(response.status), fieldErrors);
  }
  return data;
}
