# Dnp.S3.Manager

A lightweight Windows Forms S3 manager for uploading, downloading, and managing objects in S3-compatible storage.

This repository contains a desktop UI (`Dnp.S3.Manager.WinForms`), a small S3 client library (`Dnp.S3.Manager.Lib`), and unit tests. The application persists configuration, accounts, and logs in a single application SQLite database (`app.db`) and protects account secrets using DPAPI (Windows Data Protection API).

## Key features

- ? UI for browsing buckets and objects
- ? Upload / download with progress and cancellation
- ? Concurrent transfer queue with configurable concurrency
- ?? Accounts and secrets persisted in `app.db` (SQLite) and encrypted with DPAPI (`ProtectedData`)
- ?? Unit tests (xUnit) cover DB-backed account storage and DPAPI roundtrip
- ?? Single application DB contains `Accounts`, `Settings`, and `Logs` tables


## Getting started (development)

1. Restore and build:

   ```bash
   dotnet restore
   dotnet build
   ```

2. Run the WinForms application from Visual Studio or using `dotnet run` against the WinForms project.

3. The application uses an application-scoped SQLite DB file located under `%APPDATA%/Dnp.S3.Manager/app.db` by default. The code constructs and initializes the DB schema on first run.

## Configuration and persistence

- Account `SecretKey` values are encrypted using DPAPI before being written to the DB. The app decrypts secrets when loading accounts.

## Running tests

- Run unit tests with:

  ```bash
  dotnet test
  ```

- Tests are DB-only and create temporary SQLite files during execution. Tests clean up temporary DB files automatically.

## Future enhancements

- Add optional image assets in `assets/` and support richer README visuals (SVG icons for feature bullets)
- Add an optional cross-machine secret provider (Azure Key Vault or other) as an alternative to DPAPI
- Add integration tests that exercise an S3-compatible test server (localstack or minio) in CI
- Improve logging and diagnostics (more structured log properties in the `Logs` table)
- Add graceful migration path from older legacy stores if required (optional migration utility)

- Add configurable navigation modes for the bucket contents view: support immediate navigation on single selection, require double-click to open folders, or restrict navigation to keyboard actions; expose a persisted user setting.
- Add a setting to toggle confirm prompts when navigating into folders (useful for large folders) and an option to disable automatic navigation for programmatic selections.

## Notes for contributors

- Follow the DB-first approach: use `AccountsSqlStore` to ensure schema and to load accounts. Do not add new file-based persistence.
- Keep secrets encrypted at rest. Use `System.Security.Cryptography.ProtectedData` (DPAPI) for secrets unless a different provider is introduced.


## TODO (refactor)

- TODO: DatagridView row hight so that the text is not cut off. Also, the logs are not showing up in the datagridview. Need to fix that.
- TODO: Refactor Account Form — align controls, fonts, spacing and toolbar/buttons with Main form for consistent look and feel.
- TODO: Refactor Settings Form — align layout and behavior with Main form; unify Save/Cancel placement and iconography, and standardize margins/paddings.
- TODO: Other ease of use updates, check tab order for the pages.
- TODO: Add log filtering and searching capabilities.
- TODO: Add support for multiple concurrent transfers with a transfer queue and configurable concurrency level.
- TODO: Add support for drag-and-drop file uploads and downloads.


