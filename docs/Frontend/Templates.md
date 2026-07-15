# Templates — Frontend

Anchor file for the Templates + Bookmarks feature's frontend documentation, referenced from
`docs/DOCUMENT_INDEX.md`. Bookmarks (admin-curated merge-field snippets) are documented as part
of Templates rather than as a separate manifest entry, since their only purpose is inserting
snippets into a template/message body. No implementation detail is duplicated here — see:

- `CODEBASE_MAP.md` §6, legacy `TemplatesPage.tsx` (list + create form + inline per-row edit).
- `CODEBASE_MAP.md` §6a, `TemplatesPageV2.tsx` — list/live-preview split pane, Active/Inactive/
  All filter, "Insert bookmark" dropdown, `merge-field-text.tsx`'s raw-source/sample-preview
  toggle (`MergeFieldText`).
- `CODEBASE_MAP.md` §6a, Settings → Template tab (`components/settings/template-tab.tsx`,
  backed by `hooks/useBookmarks.ts`) — Bookmark CRUD table, Admin-only.
- `CODEBASE_MAP.md` §6a, command palette's "New template" (`quick-create-template-form.tsx`).
- `PROJECT_CONTEXT.md` FR-001, BR-013 for the functional spec and business rules.

Update this file only when Templates-frontend documentation needs to say something
`CODEBASE_MAP.md` doesn't already cover — otherwise just keep the cross-reference current.
