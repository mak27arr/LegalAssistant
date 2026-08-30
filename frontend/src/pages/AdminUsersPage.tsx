import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { useAuth } from '../features/auth/AuthContext';
import { getAdminRoles, getAdminUsers, updateAdminUserRoles } from '../shared/api/client';
import type { AdminRoleResponse, AdminUserResponse } from '../shared/types/api';

type DraftState = Record<string, string[]>;

export function AdminUsersPage() {
  const { user } = useAuth();
  const [users, setUsers] = useState<AdminUserResponse[]>([]);
  const [roles, setRoles] = useState<AdminRoleResponse[]>([]);
  const [drafts, setDrafts] = useState<DraftState>({});
  const [savingUserId, setSavingUserId] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    void loadData();
  }, []);

  async function loadData() {
    setIsLoading(true);
    setError(null);

    try {
      const [loadedUsers, loadedRoles] = await Promise.all([getAdminUsers(), getAdminRoles()]);
      setUsers(loadedUsers);
      setRoles(loadedRoles);
      setDrafts(Object.fromEntries(loadedUsers.map((loadedUser) => [loadedUser.id, [...loadedUser.roles]])));
    } catch (requestError) {
      setError(requestError instanceof Error ? requestError.message : 'Unable to load user administration data.');
    } finally {
      setIsLoading(false);
    }
  }

  function toggleRole(userId: string, roleName: string) {
    setDrafts((current) => {
      const currentRoles = current[userId] ?? [];
      const hasRole = currentRoles.includes(roleName);
      const nextRoles = hasRole
        ? currentRoles.filter((candidate) => candidate !== roleName)
        : [...currentRoles, roleName];

      return {
        ...current,
        [userId]: nextRoles
      };
    });
  }

  async function saveRoles(targetUser: AdminUserResponse) {
    setSavingUserId(targetUser.id);
    setError(null);

    try {
      const updated = await updateAdminUserRoles(targetUser.id, drafts[targetUser.id] ?? []);
      setUsers((current) => current.map((candidate) => candidate.id === updated.id ? updated : candidate));
      setDrafts((current) => ({
        ...current,
        [updated.id]: [...updated.roles]
      }));
    } catch (requestError) {
      setError(requestError instanceof Error ? requestError.message : 'Unable to update user roles.');
    } finally {
      setSavingUserId(null);
    }
  }

  function hasDraftChanges(targetUser: AdminUserResponse) {
    const draftRoles = [...(drafts[targetUser.id] ?? [])].sort().join('|');
    const currentRoles = [...targetUser.roles].sort().join('|');
    return draftRoles !== currentRoles;
  }

  return (
    <section className="panel">
      <div className="panel-header">
        <div>
          <p className="eyebrow">Admin Only</p>
          <h2>User management</h2>
          <p>Review signed-in users and assign the roles that control access to the operator console.</p>
        </div>
        <span className="code-chip">GET /api/admin/users</span>
      </div>

      <div className="button-row">
        <Link className="button-secondary" to="/">
          Back to intake
        </Link>
        <button className="button-secondary" type="button" onClick={() => void loadData()}>
          {isLoading ? 'Refreshing...' : 'Refresh users'}
        </button>
      </div>

      {error ? <div className="inline-error">{error}</div> : null}

      {isLoading ? (
        <div className="inline-info">Loading users and roles...</div>
      ) : users.length === 0 ? (
        <div className="inline-info">No users have signed in yet.</div>
      ) : (
        <div className="table-shell">
          <table className="data-table">
            <thead>
              <tr>
                <th>User</th>
                <th>Current roles</th>
                <th>Edit roles</th>
                <th>Last login</th>
                <th>Actions</th>
              </tr>
            </thead>
            <tbody>
              {users.map((managedUser) => {
                const draftRoles = drafts[managedUser.id] ?? managedUser.roles;
                const isSelf = user?.id === managedUser.id;

                return (
                  <tr key={managedUser.id}>
                    <td>
                      <Link className="entity-link" to={`/admin/users/${managedUser.id}`}>
                        <strong>{managedUser.fullName}</strong>
                      </Link>
                      <div className="table-subtitle">{managedUser.email}</div>
                      <div className="table-subtitle">
                        {managedUser.isActive ? 'Active account' : 'Blocked account'}
                      </div>
                    </td>
                    <td>
                      <div className="role-pill-list">
                        {managedUser.roles.map((role) => (
                          <span className="role-pill" key={`${managedUser.id}-current-${role}`}>
                            {role}
                          </span>
                        ))}
                      </div>
                    </td>
                    <td>
                      <div className="role-editor">
                        {roles.map((role) => {
                          const checked = draftRoles.includes(role.name);
                          const disableToggle = isSelf && role.name === 'Admin';

                          return (
                            <label className="role-checkbox" key={`${managedUser.id}-${role.id}`}>
                              <input
                                checked={checked}
                                disabled={disableToggle || savingUserId === managedUser.id}
                                type="checkbox"
                                onChange={() => toggleRole(managedUser.id, role.name)}
                              />
                              <span>{role.name}</span>
                            </label>
                          );
                        })}
                      </div>
                    </td>
                    <td>{managedUser.lastLoginAt ? new Date(managedUser.lastLoginAt).toLocaleString() : 'Never'}</td>
                    <td>
                      <div className="button-row">
                        <Link className="button-secondary button-inline" to={`/admin/users/${managedUser.id}`}>
                          Open details
                        </Link>
                        <button
                          className="button-primary"
                          disabled={!hasDraftChanges(managedUser) || savingUserId === managedUser.id}
                          type="button"
                          onClick={() => void saveRoles(managedUser)}
                        >
                          {savingUserId === managedUser.id ? 'Saving...' : 'Save roles'}
                        </button>
                      </div>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
}
