# TODO

## Presentation / State Sync

- Notification currently follows real runtime state immediately. Some action-result notifications can appear before the action presentation finishes, while the visible UI is still showing the previous displayed snapshot. Later, consider routing notifications through the same presentation/adopt timing model, or buffering action-scoped notifications until presentation completion.
