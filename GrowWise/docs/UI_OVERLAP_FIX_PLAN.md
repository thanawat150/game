# UI overlap fix

- Remove the duplicate bottom inventory line from the visible remaster HUD by painting one dedicated toolbar header strip.
- Keep transient notifications in a separate row above the toolbar.
- Render remaining stock inside the toolbar header row so it cannot cover dialogs or notifications.
- Validate at the project viewport size and Windows export.
