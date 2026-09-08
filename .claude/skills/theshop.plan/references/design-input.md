# Design input


- **MudBlazor MCP** — call when the spec implies UI components and you need to pick the right one or verify a parameter. Use `mudblazor:search_components`, `mudblazor:get_component_detail`, or `mudblazor:get_component_parameters`. Don't enumerate all components "for completeness."
- **Figma MCP** — call when the spec describes a designed UI flow. The downstream `shop-ui-implementer` agent will **re-fetch these nodes at implementation time** to translate them with high fidelity, so the plan must capture three things explicitly:

  1. The **Figma file URL** (the canonical link).
  2. The **per-page/component node ID** for every page or component this feature introduces or modifies — node IDs are the contract the UI implementer reads.
  3. A **one-sentence "visual intent"** per node — what the node is and how it fits into the feature flow. Not a re-description of the design; a hook so a reader (or the implementer) can confirm "this is the sign-in OTP step, not the sign-up first step."

  **How to find node IDs — follow this sequence:**

  **a. If the user supplied `--figma`** — use the file key and/or node IDs directly from their input. Call `figma-console:figma_get_component_for_development` on each provided node ID to confirm it exists and get its children. Use those children as the per-component node IDs for the plan.

  **b. If no `--figma` was supplied and the spec implies UI** — ask the user once before fetching:

  > "This feature has a UI phase. Do you have a Figma link or node ID for it? If yes, provide it now (e.g., `https://figma.com/...` or `123:456`). If no, I'll search the open Figma file for a page matching this feature — reply `skip` to skip Figma entirely for this feature."

  Wait for the reply. Then:
  - **URL/node ID given** — proceed as in (a).
  - **`skip` or no Figma** — omit the "Figma references" subsection from Section 7 Step 5 (Web) and add an open question in Section 11: "Figma node IDs not provided — `shop-ui-implementer` will need them before building the UI."
  - **No file open in Figma** — if the Figma MCP returns no open file, note it and surface as an open question in Section 11.

  **c. Extracting per-component node IDs** — once you have a starting frame or page node:
  1. Call `figma-console:figma_get_component_for_development` on the top-level frame to get direct children.
  2. For each child that maps to a distinct page or major component in the feature (e.g., "Sign-in form", "OTP step", "Error state"), record its node ID and a one-sentence visual intent.
  3. Do not go deeper than one level of children unless a child is itself a complex nested component that warrants its own node ID entry.

  Capture all of this in Section 7 Step 5 (Web) of the plan using the **Figma references** subsection (see the template). If a node ID is missing or ambiguous, surface it as an open question in Section 11 — do not paper over it. Don't call Figma for non-UI features.

Both are skippable for backend-only or domain-rule features.

