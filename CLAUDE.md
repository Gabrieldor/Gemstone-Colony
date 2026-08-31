# CLAUDE.md — Gemstone Colony

Project-specific instructions for Claude Code. These OVERRIDE default behavior and must be followed exactly.

## Project

- **Engine:** Unity 6000.5.4f1 (Unity 6), URP (3D).
- **Status:** Fresh project scaffold — no gameplay code yet.
- **Docs live in:** `/Docs` (GDD, checklists — see below). Create this folder on first use if missing.

## Architecture: SOLID / GRASP first

- Every new script or refactor must be designed against SOLID (SRP, OCP, LSP, ISP, DIP) and GRASP (Information Expert, Creator, Controller, Low Coupling, High Cohesion, Polymorphism, Protected Variations, Indirection, Pure Fabrication) principles — as far as Unity's component/MonoBehaviour model reasonably allows.
- If a clean SOLID/GRASP-compliant design is genuinely not possible or not practical in Unity (e.g. Inspector serialization constraints, MonoBehaviour instantiation rules), **say so explicitly and explain the tradeoff** before implementing — don't silently compromise and don't force an over-engineered abstraction just to satisfy the letter of a principle.
- Prefer composition over inheritance, interfaces for cross-system contracts, and small single-purpose components/classes over monoliths.

## Before implementing

- Whenever asked for an implementation, **explain what you're about to do first** (approach, files/scripts touched, key design decisions) before writing code. Don't implement silently.
- After finishing a new implementation, **ask whether to commit and push to git** — never commit/push automatically. Scope the commit to that implementation only.

## No AI-generated art

- **Never generate AI art** (images, textures, sprites, icons, 3D model textures, etc.) for this project, via any tool or MCP, under any circumstance. If art assets are needed, ask the user to supply them or point at an existing asset in the project/Asset Store.

## Performance

- Always prioritize runtime performance in implementation choices: minimize allocations (especially per-frame/GC pressure), avoid unnecessary `Update()` usage, prefer object pooling for frequently spawned objects, be mindful of draw calls/batching, and choose efficient data structures/algorithms. Flag any deliberate perf/readability tradeoff.

## Unity interaction: MCP only

- **Never interact with the Unity Editor directly** (no manual file edits to scenes/prefabs assuming Editor state, no guessing at Editor behavior). All Editor operations — GameObject creation, component wiring, scene changes, running tests, etc. — must go through the Unity MCP tools.
- Use the `unity-mcp-skill` and `unity-hierarchy-editor` skills for any hierarchy/scene/UI work — don't bypass them.

## UI work

Whenever a task involves UI (Canvas panels, buttons, HUD, inventory, dialogs, etc.), for **every element inserted**:
1. Validate its position/anchors/size against sibling elements to avoid bad overlap or clipping.
2. Take a screenshot (via MCP) after placement to visually confirm it looks correct.
3. Iterate if the screenshot shows a problem — don't declare UI work done from code/hierarchy state alone.

## Testing / verification

- After implementing a requested feature or fix, verify it actually achieves what was asked — via Unity MCP (Play Mode checks, Unity Test Framework tests, screenshots) rather than just asserting it should work from reading the code.
- If something can't be verified this way, say so explicitly instead of claiming success.

## Documentation

- Maintain a **GDD (Game Design Document)** and **task checklists** in `/Docs` to track design decisions and outstanding work. Keep them updated as scope evolves — don't let them go stale.
- Suggested structure:
  - `Docs/GDD.md` — game concept, systems, mechanics, design decisions and rationale.
  - `Docs/Checklist.md` — task/feature checklist, grouped by system/milestone.
- When a task changes design direction or adds/removes scope, update the GDD and checklist as part of that task, not as an afterthought.
