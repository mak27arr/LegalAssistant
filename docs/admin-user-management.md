# Admin User Management

Date: 2026-08-30

## Scope

The admin module is available only to users with the `Admin` role.

Implemented capabilities:

- list users
- filter users by status
- search users by name or email
- sort users
- paginate users
- view a single user
- list available roles
- update user roles
- block a user
- unblock a user

## API

- `GET /api/admin/users`
  Supports query params: `search`, `status`, `sort`, `page`, `pageSize`
- `GET /api/admin/users/{userId}`
- `GET /api/admin/roles`
- `PUT /api/admin/users/{userId}/roles`
- `POST /api/admin/users/{userId}/block`
- `POST /api/admin/users/{userId}/unblock`

All endpoints require:

- authenticated user
- `Admin` role

## Frontend

The admin UI is available at:

- `/admin/users`
- `/admin/users/{userId}`

The navigation link is shown only for authenticated users who have the `Admin` role.

## Current Role Rules

- supported roles are `User` and `Admin`
- `User` is always enforced as a base role during role updates
- an admin cannot remove the `Admin` role from their own account through the UI or API
- an admin cannot block their own account through the UI or API
- a blocked user cannot complete Google sign-in
- a blocked user cannot continue using existing application sessions
- cookie-authenticated requests are rejected when the database marks the user as blocked

## Notes

- bootstrap admins created from configured emails continue to work with the same auth flow
- the user details page exposes account identity, timestamps, Google subject id, current roles, and account status
