import { useRef, useState, type FormEvent } from 'react';
import { Navigate, useLocation } from 'react-router-dom';
import { authApi, type LoginResponse } from '../../api/auth';
import { ApiError } from '../../api/client';
import { saveSession, useSession } from '../../api/session';
import { safeReturnPath } from './returnPath';
import { Field } from '../../components/Field';

interface LoginErrors {
  email?: string;
  password?: string;
}

export function LoginPage() {
  const session = useSession();
  const location = useLocation();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [errors, setErrors] = useState<LoginErrors>({});
  const [error, setError] = useState<string>();
  const [pending, setPending] = useState(false);
  const submitting = useRef(false);

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (submitting.current) return;
    const trimmedEmail = email.trim();
    const nextErrors: LoginErrors = {};
    if (!trimmedEmail) nextErrors.email = 'Email is required.';
    else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(trimmedEmail)) {
      nextErrors.email = 'Enter a valid email address.';
    }
    if (!password) nextErrors.password = 'Password is required.';
    setErrors(nextErrors);
    setError(undefined);
    if (Object.keys(nextErrors).length) return;

    submitting.current = true;
    setPending(true);
    try {
      const result: LoginResponse = await authApi.login({ email: trimmedEmail, password });
      saveSession(result);
      setPassword('');
    } catch (caught) {
      if (caught instanceof ApiError) {
        setError(caught.status === 401 ? 'Invalid email or password.' :
          caught.message || 'The server could not complete sign in. Please try again.');
      } else {
        setError('Could not connect to the support desk. Please try again.');
      }
    } finally {
      submitting.current = false;
      setPending(false);
    }
  }

  if (session) return <Navigate to={safeReturnPath(location.state)} replace />;

  return <section className="card login-panel" aria-labelledby="login-heading">
    <h1 id="login-heading">Agent sign in</h1>
    <p>Sign in with your support agent account.</p>
    <form className="form" noValidate onSubmit={(event) => void submit(event)}>
      <Field id="login-email" label="Email" error={errors.email}>
        {(props) => <input {...props} type="email" autoComplete="username" required
          value={email} disabled={pending} onChange={(event) => setEmail(event.target.value)} />}
      </Field>
      <Field id="login-password" label="Password" error={errors.password}>
        {(props) => <input {...props} type="password" autoComplete="current-password" required
          value={password} disabled={pending} onChange={(event) => setPassword(event.target.value)} />}
      </Field>
      {error && <p role="alert" className="field__error">{error}</p>}
      <button className="button button--primary" type="submit" disabled={pending}>
        {pending ? 'Signing in...' : 'Sign in'}
      </button>
    </form>
  </section>;
}
