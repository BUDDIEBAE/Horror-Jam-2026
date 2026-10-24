# Implementation Plan Guidelines

Whenever creating an Implementation Plan broken into Phases:
1. **Always include detailed testing and verification steps inside EACH Phase:**
   - Concrete, step-by-step actions the user or tester should take in the editor/game.
   - Specific visual or behavioral cues to verify (Pass Criteria).
   - Negative checks (Fail Criteria / What must NOT happen, e.g. no null refs, pink shaders, black screens).
2. **Present plans as a user-facing Markdown artifact (`.md`)** with `RequestFeedback = true` so the user can review and approve it before execution.
