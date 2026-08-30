import { createContext, useContext, useEffect, useState } from 'react';
import {
  getAuthConfig,
  getCurrentUser,
  logout as logoutRequest,
  refreshAccessToken
} from '../../shared/api/client';
import { getAccessToken, setAccessToken } from './session';
import type { AuthConfigResponse, AuthMeResponse } from '../../shared/types/api';

type AuthStatus = 'loading' | 'authenticated' | 'anonymous';

interface AuthContextValue {
  status: AuthStatus;
  user: AuthMeResponse | null;
  config: AuthConfigResponse | null;
  isAdmin: boolean;
  beginGoogleLogin: () => void;
  refreshSession: () => Promise<boolean>;
  logout: () => Promise<void>;
}

const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [status, setStatus] = useState<AuthStatus>('loading');
  const [user, setUser] = useState<AuthMeResponse | null>(null);
  const [config, setConfig] = useState<AuthConfigResponse | null>(null);

  useEffect(() => {
    void initialize();
  }, []);

  async function initialize() {
    try {
      const authConfig = await getAuthConfig();
      setConfig(authConfig);
    } catch {
      setConfig(null);
    }

    const restored = await refreshSessionInternal();
    if (!restored) {
      setStatus('anonymous');
    }
  }

  async function refreshSessionInternal() {
    try {
      const tokenResponse = await refreshAccessToken();
      setAccessToken(tokenResponse.accessToken);
      const currentUser = await getCurrentUser();
      setUser(currentUser);
      setStatus('authenticated');
      return true;
    } catch {
      setAccessToken(null);
      setUser(null);
      setStatus('anonymous');
      return false;
    }
  }

  function beginGoogleLogin() {
    const loginUrl = config?.providers.google.loginUrl;
    if (!loginUrl) {
      return;
    }

    window.location.assign(loginUrl);
  }

  async function logout() {
    try {
      await logoutRequest();
    } finally {
      setAccessToken(null);
      setUser(null);
      setStatus('anonymous');
    }
  }

  const value: AuthContextValue = {
    status,
    user,
    config,
    isAdmin: Boolean(user?.roles.includes('Admin')),
    beginGoogleLogin,
    refreshSession: refreshSessionInternal,
    logout
  };

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error('useAuth must be used within AuthProvider.');
  }

  return context;
}

export function hasAccessToken() {
  return Boolean(getAccessToken());
}
