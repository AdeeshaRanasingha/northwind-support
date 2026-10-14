import { useSyncExternalStore } from 'react';

export interface AuthSession {
  accessToken: string;
  expiresAtUtc: string;
}

export const sessionStorageKey = 'supportdesk.session';
const listeners = new Set<() => void>();
let cachedRaw: string | null = null;
let cachedSession: AuthSession | null = null;
let expirationTimer: ReturnType<typeof setTimeout> | undefined;

function isValid(value: unknown): value is AuthSession {
  if (typeof value !== 'object' || value === null) return false;
  return 'accessToken' in value && typeof value.accessToken === 'string'
    && value.accessToken.trim().length > 0
    && 'expiresAtUtc' in value && typeof value.expiresAtUtc === 'string'
    && Number.isFinite(Date.parse(value.expiresAtUtc))
    && Date.parse(value.expiresAtUtc) > Date.now();
}

function notify() {
  listeners.forEach((listener) => listener());
}

function scheduleExpiration(session: AuthSession | null) {
  clearTimeout(expirationTimer);
  if (session) {
    expirationTimer = setTimeout(() => {
      if (getSession()) scheduleExpiration(getSession());
      else notify();
    }, Math.min(Date.parse(session.expiresAtUtc) - Date.now(), 2_147_483_647));
  }
}

export function getSession(): AuthSession | null {
  const raw = sessionStorage.getItem(sessionStorageKey);
  if (raw === cachedRaw && cachedSession && isValid(cachedSession)) return cachedSession;
  let value: unknown = null;
  try { value = raw ? JSON.parse(raw) : null; } catch { /* Discard malformed storage. */ }
  if (!isValid(value)) {
    sessionStorage.removeItem(sessionStorageKey);
    cachedRaw = null;
    cachedSession = null;
    clearTimeout(expirationTimer);
    return null;
  }
  cachedRaw = raw;
  cachedSession = { accessToken: value.accessToken, expiresAtUtc: value.expiresAtUtc };
  scheduleExpiration(cachedSession);
  return cachedSession;
}

export function saveSession(session: AuthSession): void {
  if (!isValid(session)) {
    clearSession();
    throw new Error('The server returned an invalid or expired session.');
  }
  // Persist only credentials needed for requests, never the login password.
  sessionStorage.setItem(sessionStorageKey, JSON.stringify({
    accessToken: session.accessToken,
    expiresAtUtc: session.expiresAtUtc,
  }));
  getSession();
  notify();
}

export function clearSession(): void {
  sessionStorage.removeItem(sessionStorageKey);
  cachedRaw = null;
  cachedSession = null;
  clearTimeout(expirationTimer);
  notify();
}

function subscribe(listener: () => void) {
  listeners.add(listener);
  const storageChanged = (event: StorageEvent) => {
    if (event.storageArea === sessionStorage && (event.key === sessionStorageKey || event.key === null)) {
      getSession();
      notify();
    }
  };
  window.addEventListener('storage', storageChanged);
  return () => {
    listeners.delete(listener);
    window.removeEventListener('storage', storageChanged);
  };
}

export function useSession(): AuthSession | null {
  return useSyncExternalStore(subscribe, getSession, () => null);
}
