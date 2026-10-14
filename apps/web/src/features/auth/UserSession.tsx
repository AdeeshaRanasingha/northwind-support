import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { authApi, type CurrentAgent } from '../../api/auth';
import { clearSession, useSession } from '../../api/session';

export function UserSession() {
  const session = useSession();
  const navigate = useNavigate();
  const [identity, setIdentity] = useState<{ token: string; agent: CurrentAgent }>();
  const [failedToken, setFailedToken] = useState<string>();

  useEffect(() => {
    if (!session) return;
    const controller = new AbortController();
    void authApi.me(controller.signal).then((agent) => {
      if (!controller.signal.aborted) setIdentity({ token: session.accessToken, agent });
    }).catch(() => {
      if (!controller.signal.aborted) setFailedToken(session.accessToken);
    });
    return () => controller.abort();
  }, [session]);

  if (!session) return null;
  const name = identity?.token === session.accessToken ? identity.agent.fullName : undefined;
  return <div className="app__session">
    <span>{name ? `Signed in as ${name}` : failedToken === session.accessToken
      ? 'Agent information unavailable' : 'Loading agent...'}</span>
    <button className="button" onClick={() => {
      clearSession();
      navigate('/login', { replace: true });
    }}>Logout</button>
  </div>;
}
