# Inbox — Frontend

Anchor file for the Inbox feature's frontend documentation, referenced from
`docs/DOCUMENT_INDEX.md`. No implementation detail is duplicated here — see:

- `CODEBASE_MAP.md` §6 (legacy `InboxPage.tsx`/`ConversationPanel.tsx`) and §6a (v2 redesign
  equivalents) for components, hooks (`useThreads.ts`, `useInboxHub.ts`), and SignalR wiring.
- `CODEBASE_MAP.md` §6e for the responsive-design pass over these screens.
- `PROJECT_CONTEXT.md` FR-005/FR-007 for the functional spec (inbound routing, real-time
  shared inbox).

Update this file only when Inbox-frontend documentation needs to say something
`CODEBASE_MAP.md` doesn't already cover — otherwise just keep the cross-reference current.
