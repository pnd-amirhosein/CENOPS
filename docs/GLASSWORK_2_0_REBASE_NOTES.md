# Glasswork 2.0 — Chat Backend (rebased onto your `project_1_9.zip`)

This deliverable starts from the uploaded `project_1_9.zip` and includes all its files without changing your custom Skyline, DataTable, Knowledge Library, Home, Login, CSS, `.csproj`, or `.idea` files.

It adds the same chat backend as the original Glasswork 2.0: rooms / direct-message deduplication, room members, message CRUD, encryption, SignalR, per-room membership restrictions, `chat.use` permissions, and authorizations by username. No chat UI was introduced.

## Important migration difference

Your uploaded project has a newly consolidated `src/HealthMonitoringApp.Infrastructure/Migrations/20261008130528_Initial.cs`, **not** the legacy `Persistence/Migrations` chain. We preserved it and created `src/HealthMonitoringApp.Infrastructure/Migrations/20261011103000_AddChatBackend.cs`, updating **your** `Migrations/AppDbContextModelSnapshot.cs` in place.

If the deployment database already contains an older migration history rather than `20261008130528_Initial`, reconcile it with the consolidated migration before running any update. Back up the DB first.

## Setup

1. Configure a persistent `Chat:KeyRingPath` with read/write permission for the app account and secure backups (Data Protection encryption keys).
2. From the solution root, run `dotnet build`.
3. Verify migration history in a backup/test DB; then run:
   `dotnet ef database update --project src/HealthMonitoringApp.Infrastructure --startup-project src/HealthMonitoringApp.WebUI`
4. Sign in to an existing authorized account and connect to authenticated SignalR hub `/hubs/chat`.
5. Run a two-user real-time test of rooms, DM uniqueness, membership privacy, and encrypted message persistence.

See `CHAT_BACKEND_2_0.md` for full SignalR and service API documentation.

## Validation caveat

Source-level/diff/ZIP checks are performed in this environment; the .NET SDK may not be installed. Successful compilation, EF migration execution, SQL Server, and full SignalR integration **must** be verified locally.
