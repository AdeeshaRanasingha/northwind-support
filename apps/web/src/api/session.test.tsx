import { act, cleanup, render, screen } from '@testing-library/react';
import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { clearSession, getSession, saveSession, sessionStorageKey, useSession } from './session';

const validSession = { accessToken: 'test-token', expiresAtUtc: '2030-01-01T01:00:00Z' };

beforeEach(() => {
  vi.useFakeTimers();
  vi.setSystemTime(new Date('2030-01-01T00:00:00Z'));
  clearSession();
});
afterEach(() => { cleanup(); clearSession(); vi.useRealTimers(); });

it('restores a valid session directly from tab storage with a stable React snapshot', () => {
  sessionStorage.setItem(sessionStorageKey, JSON.stringify(validSession));
  expect(getSession()).toEqual(validSession);
  expect(getSession()).toBe(getSession());
});

it.each(['not json', '{}', 'null', JSON.stringify({ accessToken: '', expiresAtUtc: validSession.expiresAtUtc }),
  JSON.stringify({ accessToken: 'test', expiresAtUtc: 'invalid' }),
  JSON.stringify({ accessToken: 'test', expiresAtUtc: '2030-01-01T00:00:00Z' })])(
  'clears malformed or expired session: %s', (value) => {
    sessionStorage.setItem(sessionStorageKey, value);
    expect(getSession()).toBeNull();
    expect(sessionStorage.getItem(sessionStorageKey)).toBeNull();
  },
);

it('synchronizes React on login, expiration and clear', () => {
  function Consumer() {
    const session = useSession();
    return <p>{session ? 'Signed in' : 'Signed out'}</p>;
  }
  render(<Consumer />);
  act(() => saveSession(validSession));
  expect(screen.getByText('Signed in')).toBeInTheDocument();
  act(() => vi.advanceTimersByTime(3_600_000));
  expect(screen.getByText('Signed out')).toBeInTheDocument();
  expect(sessionStorage.getItem(sessionStorageKey)).toBeNull();
  act(() => saveSession({ ...validSession, expiresAtUtc: '2030-01-01T02:00:00Z' }));
  act(() => clearSession());
  expect(screen.getByText('Signed out')).toBeInTheDocument();
});
