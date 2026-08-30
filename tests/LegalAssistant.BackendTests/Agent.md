# Backend Test Rules

## Scope

This test project is for backend unit tests only.

## Required Rules

- Use `xUnit` as the test framework.
- Use `Moq` for mocking dependencies.
- Write unit tests only.
- Do not add integration tests to this project.
- Do not use a real database in tests.
- Do not use containerized services in tests.
- Do not call external APIs, queues, or file storage in tests.

## Test Boundaries

- Test business logic in isolation.
- Mock service dependencies at the boundaries.
- Keep tests deterministic and fast.
- Prefer one clear behavior per test.
- Use descriptive test names that explain the scenario and expected result.

## Allowed

- `xUnit`
- `Moq`
- in-memory object setup for pure domain or application logic
- controller tests with mocked services
- service tests with mocked collaborators

## Not Allowed

- real PostgreSQL
- real Redis
- real RabbitMQ
- real HTTP calls
- Docker-dependent tests
- end-to-end tests
- UI tests

## Structure

Recommended folder structure:

- `Admin/`
- `Auth/`
- `Common/`

Group tests by feature or service under test.

## Naming

Use names like:

- `MethodName_ShouldDoSomething_WhenCondition`
- `CreateAccessToken_ShouldIncludeRoleClaims_WhenUserHasMultipleRoles`

## Goal

Tests in this project should validate backend behavior quickly, predictably, and without infrastructure dependencies.
