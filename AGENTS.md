# Agent Instructions

- Use issue branches and pull requests for issue work; do not commit directly to `main`.
- Keep `.claude/` out of commits unless explicitly requested.
- When requesting shell escalation, prefer reusable scoped prefixes matching `.codex/rules/default.rules`.
- Do not request broad command prefixes such as plain `git`, `gh`, `npm`, or `dotnet`.
- Avoid destructive commands unless the user explicitly asks for them.
