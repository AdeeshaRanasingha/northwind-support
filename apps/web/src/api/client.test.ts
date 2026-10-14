import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { ApiError, request } from './client';
import { clearSession, getSession, saveSession, sessionStorageKey } from './session';

const fetchMock = vi.fn<typeof fetch>();

it('clears the authenticated session on 401 and later requests omit the old token', async () => {
  saveSession({ accessToken: 'test-token', expiresAtUtc: '2030-01-01T00:00:00Z' });
  fetchMock.mockResolvedValueOnce(new Response('', { status: 401 }));
  await expect(request('/tickets')).rejects.toMatchObject({ status: 401 });
  expect(getSession()).toBeNull();
  fetchMock.mockResolvedValueOnce(new Response('{}', { status: 200 }));
  await request('/tickets');
  expect(new Headers(fetchMock.mock.calls[1][1]?.headers).has('Authorization')).toBe(false);
});

it('login 401 does not invalidate an existing session', async () => {
  saveSession({ accessToken: 'test-token', expiresAtUtc: '2030-01-01T00:00:00Z' });
  fetchMock.mockResolvedValueOnce(new Response('', { status: 401 }));
  await expect(request('/auth/login')).rejects.toMatchObject({ status: 401 });
  expect(getSession()?.accessToken).toBe('test-token');
});

it('a late 401 from an old request cannot clear a newer session', async () => {
  saveSession({ accessToken: 'old-token', expiresAtUtc: '2030-01-01T00:00:00Z' });
  let finish: ((response: Response) => void) | undefined;
  fetchMock.mockReturnValueOnce(new Promise((resolve) => { finish = resolve; }));
  const pending = request('/tickets');
  saveSession({ accessToken: 'new-token', expiresAtUtc: '2030-01-01T00:00:00Z' });
  finish?.(new Response('', { status: 401 }));
  await expect(pending).rejects.toMatchObject({ status: 401 });
  expect(getSession()?.accessToken).toBe('new-token');
});
beforeEach(() => {
  clearSession();
  fetchMock.mockReset();
  vi.stubGlobal('fetch', fetchMock);
});
afterEach(() => { clearSession(); vi.unstubAllGlobals(); });

it('attaches Bearer centrally while preserving Headers, body, method and signal', async () => {
  saveSession({ accessToken: 'test-token', expiresAtUtc: '2030-01-01T00:00:00Z' });
  fetchMock.mockResolvedValue(new Response('{"ok":true}', { status: 200 }));
  const signal = new AbortController().signal;
  const result = await request('/tickets', { method: 'POST', body: '{}', signal,
    headers: new Headers({ 'X-Request-Id': '123', 'Content-Type': 'application/custom' }) });
  const init = fetchMock.mock.calls[0][1];
  expect(new Headers(init?.headers).get('Authorization')).toBe('Bearer test-token');
  expect(new Headers(init?.headers).get('X-Request-Id')).toBe('123');
  expect(new Headers(init?.headers).get('Content-Type')).toBe('application/custom');
  expect(init).toMatchObject({ method: 'POST', body: '{}', signal });
  expect(result).toEqual({ ok: true });
});

it('never attaches authorization to login even with a saved token and caller header', async () => {
  saveSession({ accessToken: 'test-token', expiresAtUtc: '2030-01-01T00:00:00Z' });
  fetchMock.mockResolvedValue(new Response('{}', { status: 200 }));
  await request('/auth/login', { headers: { Authorization: 'Bearer stale-token' } });
  expect(new Headers(fetchMock.mock.calls[0][1]?.headers).has('Authorization')).toBe(false);
});

it('does not attach an expired stored token', async () => {
  sessionStorage.setItem(sessionStorageKey, JSON.stringify({ accessToken: 'expired', expiresAtUtc: '2000-01-01T00:00:00Z' }));
  fetchMock.mockResolvedValue(new Response('{}', { status: 200 }));
  await request('/tickets');
  expect(new Headers(fetchMock.mock.calls[0][1]?.headers).has('Authorization')).toBe(false);
  expect(sessionStorage.getItem(sessionStorageKey)).toBeNull();
});

it('preserves problem details and no-content behavior', async () => {
  fetchMock.mockResolvedValueOnce(new Response(JSON.stringify({ detail: 'Conflict reason', errors: { Reason: ['Invalid'] } }), { status: 409 }));
  await expect(request('/tickets')).rejects.toMatchObject({ status: 409, message: 'Conflict reason', fieldErrors: { Reason: ['Invalid'] } });
  fetchMock.mockResolvedValueOnce(new Response(null, { status: 204 }));
  expect(await request('/tickets')).toBeUndefined();
  fetchMock.mockResolvedValueOnce(new Response('', { status: 500, statusText: 'Server error' }));
  await expect(request('/tickets')).rejects.toBeInstanceOf(ApiError);
});
