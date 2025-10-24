Title: UX & Avalonia migration sprint
Labels: ui, avalonia, ux
Assignees: TBD
Milestone: 90-day plan: Oct 24 2025 — Jan 22 2026

Goal
----
Convert the remaining Windows Forms dialogs to Avalonia and ensure the main editor workflows work on Avalonia without platform fallbacks.

Deliverables
----
- Identify 2–4 remaining dialogs or UI bits still on WinForms.
- Convert them to Avalonia with matching behavior and tests where possible.
- Update `docs/` with migration notes and screenshots.

Success criteria
----
- Manual regression: start/stop preview, open/save map, and tile editing work on Avalonia.
- No new platform-specific code paths required for the converted dialogs.
