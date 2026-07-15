# NotifyHub — Decision Log

Chronological log of architectural, technical, and business decisions, as required by the root
`CLAUDE.md`. This log starts clean from the date of its creation — historical decisions are
**not** reconstructed here; `STATUS.md` and `docs/adr/*.md` already cover project history up to
this point.

For a decision architecturally significant enough to warrant a full write-up (rejected
alternatives, broader impact), add a numbered ADR under `docs/adr/` instead/as well (see
`0001`–`0003` for the format) and link it from the entry here rather than duplicating its
content. Use this log for everything else, including smaller technical and business calls.

Newest entries at the top. Append a new entry each time a decision is made — don't rewrite or
remove prior entries.

---

## Template

Copy this block for each new entry, fill it in, and replace the date/title.

```
## YYYY-MM-DD — <decision title>
**Decision:** <what was decided>
**Reason:** <why — the constraint, tradeoff, or requirement that drove it>
**Alternatives considered:** <what else was on the table, and why it was rejected>
**Impacted modules:** <features/files/docs affected — cross-reference docs/DOCUMENT_INDEX.md entries where relevant>
```
