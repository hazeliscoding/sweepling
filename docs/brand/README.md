# Sweepling brand

The mascot is option 1A, "Pebble", chosen on 2026-10-21 from the claude.ai design canvas "Sweepling logo": a round mint critter holding a tiny broom.

- `mark.svg` / `mark-dark.svg`: the mascot alone, transparent background. The dark variant only lightens the broom handle, which would vanish on a dark background.
- `lockup.svg` / `lockup-dark.svg`: the mascot plus the "sweepling" wordmark, for light and dark backgrounds.
- The wordmark is Fredoka SemiBold, lowercase, with -0.01em letter spacing, converted to paths because GitHub can't load web fonts inside SVGs. Use the SVGs; don't re-typeset it.
- `scripts/make_brand.py` regenerates all four from the Fredoka font (OFL); its header says how.
- The tray icon is the mark cropped tighter. It closes its eyes when there is nothing to sweep, and carries a count badge when there are leftovers.

## Design tokens

The starting tokens for the review window. Every text pair below passes WCAG AA (4.5:1).

| Token | Light | Dark | Use |
|---|---|---|---|
| `surface` | `#FFFFFF` | `#1B2124` | Window background |
| `surface-raised` | `#F4F7F6` | `#232A2E` | Group rows, section headers |
| `border` | `#DDE5E2` | `#2E373B` | Dividers |
| `text` | `#17312E` | `#E6F2EE` | Body text |
| `text-muted` | `#4A5A57` | `#A9B8B4` | Paths, ages, memory |
| `accent` | `#1F7A66` | `#7FD1BB` | Links, focus ring, ticked boxes |
| `close` | `#C2410C`, text `#FFFFFF` | `#FB8B5B`, text `#1B2124` | The Close button and the tray badge |
| `close-hover` | `#9A3412` | `#FDA47E` | Close button hover |
| `caution` | `#8A6100` | `#E9B949` | "Session still running" label |

Mascot colors, never used for text: mint `#BDE7DA`, ink `#17312E`, straw `#E9B949`, blush `#F2A69B`, dark-variant handle `#E7D7B5`.

- **Type:** the system font (Segoe UI Variable) for the UI, Cascadia Mono, falling back to Consolas, for command lines and PIDs. No bundled fonts, to keep the install small. Sizes 12, 13, 14 (body), 16, 20 and 24 px.
- **Spacing:** 4, 8, 12, 16, 24, 32 and 48 px.
- **Radius:** 4 px for controls, 8 px for the window's groups.
- **Shadows:** `0 1 2 0 #1A17312E` for raised rows, `0 4 12 0 #2417312E` for menus, `0 12 32 0 #2E17312E` for the window on light. Dark mode uses borders instead of shadows.
