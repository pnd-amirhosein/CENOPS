# Glasswork 2.2 — Chat Workspace Refinement

Built from **Glasswork 2.1**, without changing existing database entities, migrations, chat hub, or membership rules.

## UI updates
- Light CENOPS cyan/glass palette throughout. Chat remains an overlay with a blurred, click-to-close backdrop and 3% page gutters.
- Compact RTL top-right label and close/minimize buttons, animated connection-state ellipsis. No traffic-light window chrome or separate online-status heading.
- Project logo reused from Skyline, consistent inline SVG icons, compact `#` rooms with no room-list header or in-column search. Add and global search live on the left rail.
- Spotlight-like in-workspace global search. Sections: joined rooms, users, and messages in joined rooms. Clicking a user creates/opens the unique DM; clicking a message navigates to that room and highlights the result.
- DM message rows omit avatars and titles. Group messages retain them. Own messages occupy up to 70% width on the right with a right-hand accent border; others appear at left with a left border. Message text auto-detects RTL/LTR.
- Composer keeps auto-height, Enter to send, Shift+Enter for newline; removed verbose keyboard help text.

## Search privacy and scope
`IChatSearchService` checks the authenticated user and the existing `chat.use` access. Message content is decrypted only after selecting from the user's currently joined rooms; no Owner/Admin exception. Since SQL text is encrypted, search examines **up to 1,200 most recent messages** across the user's rooms and returns up to 20 message results, 12 user results, and 12 room results. This is deliberate for the local-app first iteration; full-history search would need a protected search index. Search is debounced in the UI.

## Verification checklist
1. Run `dotnet build NTSW_CENOPS.sln` locally. .NET SDK is unavailable in the packaging environment.
2. Test backdrop close, chat close, minimize and reopen.
3. Verify light palette at different viewport sizes and rooms compactness.
4. Search a colleague by username or display name; open the unique DM.
5. Search a group name and a message in a joined room; check result jump/highlight.
6. Sign in as two distinct users; verify one another's messages align left/right, including Persian and English text.
7. Confirm DM rows have no avatars/names; group rows have them.
8. Verify a non-member (including Owner/Admin) cannot search or load private room messages.

No new migration is required; the Glasswork 2.0 chat migration must already be applied.
