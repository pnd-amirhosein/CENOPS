# Glasswork 2.4 — Chat UX contrast and workflow refinement

Baseline: **Glasswork 2.3**. This is a focused presentation/interaction update. The existing Identity chat membership model, encrypted message persistence, SignalR hub and migrations have not been modified.

## Changes

1. Higher contrast project-aligned cyan/ink palette, bolder labels, and slightly larger Persian text in the chat workspace.
2. Actual chat bubbles: author side/right for the current user, left for other users; messages occupy up to 70% of the conversation width. Direct messages remain without repetitive author names/avatars.
3. New conversation picker searches **username and display name** with debounced results and excludes the authenticated user. The selection is a removable `AppBadge` chip list. One selected person creates/opens a DM; two or more selected people create a group with an optional group title. Search uses `IChatSearchService.SearchUsersAsync`, a lightweight authenticated user lookup (no encrypted message scans).
4. Global chat search adopts the visual vocabulary and component class hooks of the existing Ctrl+K QuickNavigate UI, while retaining permission-aware room/user/message search and click behavior.
5. DM and group sections are independently collapsible; DMs are above groups.
6. Left rail order: chat, new chat, spacer, search, CENOPS logo at bottom.
7. A larger message composer with a cyan integrated send button and stronger focus states; existing Enter / Shift+Enter logic is unchanged.
8. CSS link bumped to `v=2.4.0` to invalidate old cached styles.

## Important manual tests

- Run `dotnet build` with your .NET SDK (SDK unavailable in the packaging environment).
- Open the chat in a regular User session and visually compare against the rest of the light CENOPS interface.
- Toggle DM and Group accordions independently and verify reopening.
- Start a DM using a username search; start a group using display-name results for at least two other users; remove a badge before saving; verify the selected participants match the new room.
- Check right/left bubbles and automatic Persian/English text directions in group and direct rooms.
- Check global search against Ctrl+K QuickNavigate, including result selection and authorization.
- Check mobile widths and multi-line composer sending.

No new migration. All room and message access remains members-only, including Owner/Admin.
