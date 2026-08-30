import { useEffect, useState } from 'react';
import { Navigate, useNavigate, useSearchParams } from 'react-router-dom';
import { useAuth } from '../features/auth/AuthContext';

export function AuthCallbackPage() {
  const [error, setError] = useState<string | null>(null);
  const [isComplete, setIsComplete] = useState(false);
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();
  const { refreshSession } = useAuth();

  useEffect(() => {
    const authStatus = searchParams.get('auth_status');
    if (authStatus && authStatus !== 'success') {
      setError('Authentication failed. Please try again.');
      return;
    }

    void (async () => {
      const restored = await refreshSession();
      if (!restored) {
        setError('The session could not be restored after sign-in.');
        return;
      }

      setIsComplete(true);
      navigate('/', { replace: true });
    })();
  }, [navigate, refreshSession, searchParams]);

  if (isComplete) {
    return <Navigate to="/" replace />;
  }

  return (
    <section className="panel">
      <div className="panel-header">
        <div>
          <p className="eyebrow">Authentication</p>
          <h2>Completing sign-in</h2>
          <p>We are restoring the secure session from the backend.</p>
        </div>
      </div>

      {error ? <div className="inline-error">{error}</div> : <div className="inline-info">Finalizing secure sign-in...</div>}
    </section>
  );
}
