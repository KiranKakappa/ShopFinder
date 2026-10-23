// Thin wrapper around fetch for the ShopFinder API.

const TOKEN_KEY = 'shopfinder.ownerToken';

export function getToken() {
  try { return localStorage.getItem(TOKEN_KEY); } catch { return null; }
}
export function setToken(token) {
  try {
    if (token) localStorage.setItem(TOKEN_KEY, token);
    else localStorage.removeItem(TOKEN_KEY);
  } catch { /* storage unavailable: stay logged in for this tab only */ }
}

export class ApiError extends Error {
  constructor(status, message) {
    super(message);
    this.status = status;
  }
}

async function request(method, path, body) {
  const headers = { Accept: 'application/json' };
  if (body !== undefined) headers['Content-Type'] = 'application/json';
  const token = getToken();
  if (token) headers.Authorization = `Bearer ${token}`;

  let res;
  try {
    res = await fetch(path, { method, headers, body: body === undefined ? undefined : JSON.stringify(body) });
  } catch {
    throw new ApiError(0, 'Cannot reach the server. Check your connection and try again.');
  }

  if (res.status === 204) return null;
  const data = await res.json().catch(() => null);
  if (!res.ok) {
    if (res.status === 401 && token) setToken(null);
    const message = data?.error
      || (res.status === 429 ? 'Too many attempts. Please wait a minute and try again.' : `Request failed (${res.status}).`);
    throw new ApiError(res.status, message);
  }
  return data;
}

function query(params) {
  const q = new URLSearchParams();
  Object.entries(params).forEach(([k, v]) => {
    if (v !== undefined && v !== null && v !== '' && v !== false) q.set(k, String(v));
  });
  const s = q.toString();
  return s ? `?${s}` : '';
}

export const api = {
  categories: () => request('GET', '/api/categories'),
  shops: (params) => request('GET', `/api/shops${query(params)}`),
  shop: (id) => request('GET', `/api/shops/${id}`),
  deals: (params) => request('GET', `/api/deals${query(params)}`),
  reportClosed: (id) => request('POST', `/api/shops/${id}/report-closed`),

  register: (data) => request('POST', '/api/owner/register', data),
  login: (phone, password) => request('POST', '/api/owner/login', { phone, password }),
  me: () => request('GET', '/api/owner/me'),
  updateShop: (data) => request('PUT', '/api/owner/shop', data),
  setStatus: (mode) => request('POST', '/api/owner/status', { mode }),
  addProduct: (data) => request('POST', '/api/owner/products', data),
  updateProduct: (id, data) => request('PUT', `/api/owner/products/${id}`, data),
  deleteProduct: (id) => request('DELETE', `/api/owner/products/${id}`)
};
