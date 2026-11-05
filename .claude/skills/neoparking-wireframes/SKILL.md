---
name: neoparking-wireframes
description: Wireframes and low-fidelity prototypes of the NeoParking views. Use when asked to wireframe, prototype or plan a screen that consumes the API. For the visual design stage, use frontend-design instead.
---

# NeoParking wireframes

Keep the fidelity low on purpose: decide flow, content and permissions before any visual style.

## Rules

1. Base every screen on real routes. Read `src/NeoParking.Api/Endpoints/` and the matrix in `docs/specs/technical-debt-remediation.md` (D2).
2. Show on every screen the route it consumes and who can use it.
3. Use grayscale and one system font. Color and style belong to the design stage.
4. Use only fictitious data that looks fictitious: `TST0A00`, `cliente@exemplo.test`, documents already masked.
5. Keep documents, plates, ticket codes, exit reasons and e-mails out of the view's URL, logs and analytics.
6. Show documents masked by default. The full value needs Owner or Admin and a notice that the access is recorded.
7. Draw the states as well as the happy path: empty, loading, 401, 403, 404, 409 and 422, each saying what to do next.
8. Operation comes first: entry and exit take few touches, with targets of at least 44 px, and work on a gatehouse tablet, a desktop and a phone.
9. Label anything not implemented as "Spec, não implementado". Take its routes and rules from `docs/specs/product-evolution.md` and invent nothing.
10. Draw no screen for a capability that depends on an open question in the spec.

## Finish every wireframe with

- routes that have no screen;
- screens that need a route that does not exist;
- UX decisions for the user.

Save the source in `docs/wireframes/`.
