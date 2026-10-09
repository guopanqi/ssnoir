# OPEN FIELD · Research Workbench (GPT-6)

Independent UI/UX concept and visual baseline, not a redesign of the existing SSNoir research webpage.

Design direction: editorial research journal and quiet scientific workbench. Warm paper, dark ink, restrained blue accent, typography and thin diagrammatic connectors. The design deliberately avoids borrowing the existing UI or the game's visual language.

## Information architecture

- Research overview: a brief human-facing synthesis, current problem and next useful interaction.
- Experiment library: experiments organized by questions, separate research and personal feedback states.
- Experiment detail: study intent, tiny *illustrative* choice sample, evolving feedback notes.
- Research atlas: open questions and their hypothesized relationships.
- Research journal: meaningful changes in thinking, not engineering logs.

## Working interaction sample

Responsive desktop/mobile navigation, experiment filters, keyword search (`/` shortcut), cross-page navigation, atlas focus, dark mode, sample two-step choice interaction, local feedback creation and revision. Feedback and theme preference persist in the browser with localStorage.

All research content is demonstration data; the miniature choice interaction is not the real gameplay experiment. No remote writes, authentication, GitHub token input, AI inference or live experiment integration are included. Feedback revisions retain an internal local history even though the revision-history UI is not yet exposed.

Open `index.html` directly; no dependencies, build step, font CDN, or server required.