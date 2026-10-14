import { act, cleanup, fireEvent, render, screen } from '@testing-library/react';
import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { authApi, type LoginResponse } from '../../api/auth';
import { ApiError } from '../../api/client';
import { LoginPage } from './LoginPage';

vi.mock('../../api/auth', () => ({ authApi: { login: vi.fn() } }));
beforeEach(() => { vi.mocked(authApi.login).mockReset(); });
afterEach(cleanup);

function fillForm(email = 'agent@example.test', password = 'test-password') {
  fireEvent.change(screen.getByLabelText('Email'), { target: { value: email } });
  fireEvent.change(screen.getByLabelText('Password'), { target: { value: password } });
}

it('blocks required fields and malformed email without calling the API', () => {
  render(<LoginPage />);
  fireEvent.click(screen.getByRole('button', { name: 'Sign in' }));
  expect(screen.getByText('Email is required.')).toBeInTheDocument();
  expect(screen.getByText('Password is required.')).toBeInTheDocument();
  fillForm('not-an-email');
  fireEvent.click(screen.getByRole('button', { name: 'Sign in' }));
  expect(screen.getByText('Enter a valid email address.')).toBeInTheDocument();
  expect(authApi.login).not.toHaveBeenCalled();
});

it('submits trimmed email with the unchanged masked password and displays success', async () => {
  vi.mocked(authApi.login).mockResolvedValue({ accessToken: 'test-token', expiresAtUtc: '2030-01-01T00:00:00Z',
    agentId: 1, fullName: 'Alex Turner', email: 'agent@example.test' });
  render(<LoginPage />);
  fillForm('  agent@example.test  ', ' password with spaces ');
  expect(screen.getByLabelText('Password')).toHaveAttribute('type', 'password');
  fireEvent.submit(screen.getByRole('button', { name: 'Sign in' }).closest('form')!);
  expect(await screen.findByRole('status')).toHaveTextContent('Signed in as Alex Turner.');
  expect(authApi.login).toHaveBeenCalledWith({ email: 'agent@example.test', password: ' password with spaces ' });
  expect(screen.getByLabelText('Password')).toHaveValue('');
});

it.each([
  { makeError: () => new ApiError(401, 'Unauthorized'), message: 'Invalid email or password.' },
  { makeError: () => new TypeError('Failed to fetch'), message: 'Could not connect to the support desk. Please try again.' },
  { makeError: () => new ApiError(500, 'Service unavailable'), message: 'Service unavailable' },
])('displays login failure without success: $message', async ({ makeError, message }) => {
  vi.mocked(authApi.login).mockRejectedValue(makeError());
  render(<LoginPage />);
  fillForm();
  await act(async () => {
    fireEvent.click(screen.getByRole('button', { name: 'Sign in' }));
  });
  expect(screen.getByRole('alert')).toHaveTextContent(message);
  expect(screen.queryByRole('status')).not.toBeInTheDocument();
});

it('disables fields and prevents duplicate submissions while pending', async () => {
  let resolve: ((result: LoginResponse) => void) | undefined;
  vi.mocked(authApi.login).mockReturnValue(new Promise((done) => { resolve = done; }));
  render(<LoginPage />);
  fillForm();
  const form = screen.getByRole('button', { name: 'Sign in' }).closest('form')!;
  fireEvent.submit(form);
  fireEvent.submit(form);
  expect(authApi.login).toHaveBeenCalledTimes(1);
  expect(screen.getByRole('button', { name: 'Signing in...' })).toBeDisabled();
  expect(screen.getByLabelText('Password')).toBeDisabled();
  resolve?.({ accessToken: 'test-token', expiresAtUtc: '2030-01-01T00:00:00Z', agentId: 1,
    fullName: 'Alex Turner', email: 'agent@example.test' });
  expect(await screen.findByRole('status')).toBeInTheDocument();
});
