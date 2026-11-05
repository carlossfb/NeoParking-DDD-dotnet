---
name: neoparking-design-process
description: Run a real design process before any high-fidelity NeoParking screen, going through brief, sourced references, style directions and user choice, then prototype. Use when asked for visual design, high fidelity, a new look, style options or a prototype of a view. For flows and low fidelity, use neoparking-wireframes first.
---

# NeoParking design process

Work through the stages in order. Each gate is a stop: do not start the next stage until it passes. A user who explicitly asks to skip a stage can skip it; say what was skipped.

## 1. Brief

Follow `references/designer-skills/design-brief.md`. Take flows, routes and permissions from the wireframes (`neoparking-wireframes`); do not reopen them here.

**Gate:** problem, users, context of use, constraints and success criteria are written. Unknowns are listed as unknown, not assumed.

## 2. References

Find 6 to 10 real references, across direct competitors, adjacent operational tools, and sources outside the category.

For each one, record:

- the link, opened in this session;
- what was seen, and how: page text, a screenshot, or only a description by someone else;
- the principle to extract: what it does, for whom, and why it applies here.

Rules:

- Never invent a reference, a link or a detail of one. If a page could not be opened, say so and do not cite what it looks like.
- Extract principles. Do not copy layouts, brand assets, illustrations or trade dress from proprietary products.
- Include what to avoid as well as what to follow.

**Gate:** every reference has a working link and states how it was seen. Present them to the user before moving on, as a board with the links and principles.

## 3. Style directions

Build 2 to 4 directions that differ in a decision that matters: density, type, color role, or surface and depth. Palette swaps of one idea count as one direction.

Each direction has a one-sentence thesis, tokens using shadcn/ui names, a type pairing and scale, radius and depth, and the references behind it. A direction with no reference behind it says so.

Show all of them on the same one or two real screens, at comparable fidelity, with the same content. Pick the screens the decision is hardest on, such as gatehouse exit by plate. These are samples, not the prototype.

`references/designer-skills/parallel-concepts.md` applies to how the set is sized and leveled. Its rule to keep the visual language constant is for concepts that test behavior. Here the visual language is the variable, so hold flow and content constant instead.

An existing look is one of the options, judged by the same criteria, not the default.

## 4. User choice

Follow `references/designer-skills/concept-selection.md`. Write the criteria before comparing, and mark each as a threshold or a trade-off. Thresholds always include: text contrast of 4.5:1, targets of at least 44 px, and buildable with shadcn/ui.

Present the directions with what each one costs. Give a recommendation if you have one.

**Gate:** stop and wait. Only the user chooses. Silence, a background notification, or your own recommendation is not a choice. Do not start the prototype in the same turn as the options.

Save the decision in `docs/wireframes/`: the choice, the criteria, what the winner gives up, and for each rejected direction what it lost on and what would bring it back. Do not graft parts of rejected directions onto the winner unless the user asks.

## 5. Prototype

Follow `frontend-design` for craft and `neoparking-wireframes` for product rules, within the chosen direction. Where `frontend-design` says to commit to a direction of your own, the user's choice replaces it. Build on the tokens from stage 3, and do not add a new look midway.

## 6. Visual check

**Gate:** finish with a list of what was and was not checked visually.

- Say how it was rendered and how faithful that rendering is. Reading source is not a visual check.
- Report measurements where possible: overflow, target size, contrast.
- If a tool's rules forbid rendering without a request, say the result is unchecked and offer to render. Never call an unseen screen good.
