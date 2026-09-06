import { useAuth } from '../features/auth/AuthContext';

export function LoginPage() {
  const { config, beginGoogleLogin, status } = useAuth();
  const google = config?.providers.google;

  return (
    <section className="panel">
      <div className="panel-header">
        <div>
          <p className="eyebrow">Secure Access</p>
          <h2>Sign in to Legal Assistant</h2>
          <p>Authentication is handled by the backend and uses Google as the identity provider.</p>
        </div>
      </div>

      {status === 'loading' ? <div className="inline-info">Loading authentication settings...</div> : null}

      {!google?.enabled ? (
        <div className="inline-error">Google sign-in is not configured yet.</div>
      ) : (
        <div className="button-row">
          <button className="button-primary" type="button" onClick={beginGoogleLogin}>
            Continue with Google
          </button>
        </div>
      )}
    </section>
  );
}
