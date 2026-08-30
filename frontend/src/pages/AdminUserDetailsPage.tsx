import { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { useAuth } from '../features/auth/AuthContext';
import {
  blockAdminUser,
  getAdminRoles,
  getAdminUser,
  unblockAdminUser,
  updateAdminUserRoles
} from '../shared/api/client';
import type { AdminRoleResponse, AdminUserDetailsResponse } from '../shared/types/api';

export function AdminUserDetailsPage() {
  const { user } = useAuth();
  const { userId } = useParams<{ userId: string }>();
  const [managedUser, setManagedUser] = useState<AdminUserDetailsResponse | null>(null);
  const [roles, setRoles] = useState<AdminRoleResponse[]>([]);
  const [draftRoles, setDraftRoles] = useState<string[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [isSavingRoles, setIsSavingRoles] = useState(false);
  const [isUpdatingStatus, setIsUpdatingStatus] = useState(false);

  useEffect(() => {
    if (!userId) {
      setError('User id is missing.');
      setIsLoading(false);
      return;
    }

    void loadData(userId);
  }, [userId]);

  async function loadData(targetUserId: string) {
    setIsLoading(true);
    setError(null);

    try {
      const [loadedUser, loadedRoles] = await Promise.all([getAdminUser(targetUserId), getAdminRoles()]);
      setManagedUser(loadedUser);
      setRoles(loadedRoles);
      setDraftRoles([...loadedUser.roles]);
    } catch (requestError) {
      setError(requestError instanceof Error ? requestError.message : 'Unable to load user details.');
    } finally {
      setIsLoading(false);
    }
  }

  function toggleRole(roleName: string) {
    setDraftRoles((current) => current.includes(roleName)
      ? current.filter((candidate) => candidate !== roleName)
      : [...current, roleName]);
  }

  async function saveRoles() {
    if (!managedUser) {
      return;
    }

    setIsSavingRoles(true);
    setError(null);

    try {
      const updated = await updateAdminUserRoles(managedUser.id, draftRoles);
      setManagedUser((current) => current ? { ...current, roles: [...updated.roles], isActive: updated.isActive } : current);
      setDraftRoles([...updated.roles]);
    } catch (requestError) {
      setError(requestError instanceof Error ? requestError.message : 'Unable to update user roles.');
    } finally {
      setIsSavingRoles(false);
    }
  }

  async function updateBlockedState(shouldBlock: boolean) {
    if (!managedUser) {
      return;
    }

    setIsUpdatingStatus(true);
    setError(null);

    try {
      const updated = shouldBlock
        ? await blockAdminUser(managedUser.id)
        : await unblockAdminUser(managedUser.id);

      setManagedUser(updated);
      setDraftRoles([...updated.roles]);
    } catch (requestError) {
      setError(requestError instanceof Error ? requestError.message : 'Unable to update account status.');
    } finally {
      setIsUpdatingStatus(false);
    }
  }

  function hasDraftChanges() {
    if (!managedUser) {
      return false;
    }

    return [...draftRoles].sort().join('|') !== [...managedUser.roles].sort().join('|');
  }

  const isSelf = user?.id === managedUser?.id;

  return (
    <section className="panel">
      <div className="panel-header">
        <div>
          <p className="eyebrow">Admin Only</p>
          <h2>User details</h2>
          <p>Inspect account metadata, adjust roles, and block or unblock access for this user.</p>
        </div>
        <span className="code-chip">GET /api/admin/users/{'{userId}'}</span>
      </div>

      <div className="button-row">
        <Link className="button-secondary" to="/admin/users">
          Back to users
        </Link>
        <button className="button-secondary" type="button" disabled={isLoading || !userId} onClick={() => userId ? void loadData(userId) : undefined}>
          {isLoading ? 'Refreshing...' : 'Refresh details'}
        </button>
      </div>

      {error ? <div className="inline-error">{error}</div> : null}

      {isLoading ? (
        <div className="inline-info">Loading user details...</div>
      ) : !managedUser ? (
        <div className="inline-error">User was not found.</div>
      ) : (
        <div className="detail-stack">
          <div className="details-grid">
            <article className="detail-card">
              <h3>Identity</h3>
              <p className="detail-value">{managedUser.fullName}</p>
              <p className="muted">{managedUser.email}</p>
            </article>
            <article className="detail-card">
              <h3>Status</h3>
              <p className="detail-value">{managedUser.isActive ? 'Active' : 'Blocked'}</p>
              <p className="muted">{managedUser.isActive ? 'Can sign in and refresh sessions.' : 'Sign-in and refresh are denied.'}</p>
            </article>
            <article className="detail-card">
              <h3>Last login</h3>
              <p className="detail-value">{managedUser.lastLoginAt ? new Date(managedUser.lastLoginAt).toLocaleString() : 'Never'}</p>
              <p className="muted">Updated from successful Google sign-in.</p>
            </article>
            <article className="detail-card">
              <h3>Roles</h3>
              <div className="role-pill-list">
                {managedUser.roles.map((role) => (
                  <span className="role-pill" key={`details-current-${role}`}>
                    {role}
                  </span>
                ))}
              </div>
            </article>
            <article className="detail-card">
              <h3>Internal id</h3>
              <p className="detail-value">{managedUser.id}</p>
            </article>
            <article className="detail-card">
              <h3>Google subject</h3>
              <p className="detail-value">{managedUser.googleSubjectId}</p>
            </article>
            <article className="detail-card">
              <h3>Created</h3>
              <p className="detail-value">{new Date(managedUser.createdAt).toLocaleString()}</p>
            </article>
            <article className="detail-card">
              <h3>Updated</h3>
              <p className="detail-value">{new Date(managedUser.updatedAt).toLocaleString()}</p>
            </article>
          </div>

          <div className="details-grid">
            <article className="detail-card">
              <h3>Edit roles</h3>
              <div className="role-editor">
                {roles.map((role) => {
                  const checked = draftRoles.includes(role.name);
                  const disableToggle = isSelf && role.name === 'Admin';

                  return (
                    <label className="role-checkbox" key={role.id}>
                      <input
                        checked={checked}
                        disabled={disableToggle || isSavingRoles}
                        type="checkbox"
                        onChange={() => toggleRole(role.name)}
                      />
                      <span>{role.name}</span>
                    </label>
                  );
                })}
              </div>
              <div className="button-row compact-card">
                <button
                  className="button-primary"
                  disabled={!hasDraftChanges() || isSavingRoles}
                  type="button"
                  onClick={() => void saveRoles()}
                >
                  {isSavingRoles ? 'Saving...' : 'Save roles'}
                </button>
              </div>
            </article>

            <article className="detail-card">
              <h3>Access control</h3>
              <p className="muted">
                Blocking revokes active refresh tokens and prevents new Google sign-in until the account is unblocked.
              </p>
              <div className="button-row compact-card">
                {managedUser.isActive ? (
                  <button
                    className="button-danger"
                    disabled={isSelf || isUpdatingStatus}
                    type="button"
                    onClick={() => void updateBlockedState(true)}
                  >
                    {isUpdatingStatus ? 'Blocking...' : 'Block user'}
                  </button>
                ) : (
                  <button
                    className="button-primary"
                    disabled={isUpdatingStatus}
                    type="button"
                    onClick={() => void updateBlockedState(false)}
                  >
                    {isUpdatingStatus ? 'Unblocking...' : 'Unblock user'}
                  </button>
                )}
              </div>
              {isSelf ? <p className="muted">Your own account cannot be blocked from this screen.</p> : null}
            </article>
          </div>
        </div>
      )}
    </section>
  );
}
