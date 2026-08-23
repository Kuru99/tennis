# Third-party asset register

The MVP uses Unity primitives, project-authored materials, procedurally generated sound effects, and the following licensed typefaces.

## Runtime dependency

- Epic Online Services Plugin for Unity 6.1.1 (optional; network battle is currently disabled)
  - Local development package: `Packages/ThirdParty/com.playeveryware.eos-6.1.1.tgz` (excluded from Git)
  - Source: https://github.com/EOS-Contrib/eos_plugin_for_unity/releases/tag/v6.1.1
  - Author: Epic Games, Inc.
  - License: MIT; the package includes its license and third-party notices.
  - Use: Retained behind the `PRIDE_COURT_EOS` compile symbol for future EOS Connect, Lobby, and P2P work. Public builds do not include the package or credentials.

## GitHub design references (2026-08-22)

- UnityTechnologies/open-project-1: initialization, main-menu, gameplay separation and finite-state-machine documentation were reviewed as architecture references.
- Unity-Technologies/com.unity.services.samples.game-lobby: Lobby and Relay responsibilities were reviewed only to keep the future network entry separate from the current placeholder UI.
- in0finite/NetworkDiscoveryUnity (https://github.com/in0finite/NetworkDiscoveryUnity): its UDP broadcast/request-response approach and stated Windows/Android test coverage were reviewed for the LAN discovery boundary.
- GameDeveloperS001/LiteNetLib (https://github.com/GameDeveloperS001/LiteNetLib): its Unity/Android support, discovery, low-latency UDP features, and connection lifecycle were reviewed when deciding the prototype transport scope.
- Unity-Technologies/com.unity.multiplayer.docs (https://github.com/Unity-Technologies/com.unity.multiplayer.docs): Unity Transport's cross-platform UDP abstraction and reliable pipeline documentation were reviewed as the future upgrade path if WAN play or richer delivery guarantees are required.
- No source code or assets were copied from these repositories. The project implementation uses only .NET socket APIs and project-authored protocol code.

## UI typefaces

- Dela Gothic One Regular
  - File: `Assets/_Project/Resources/Fonts/DelaGothicOne-Regular.ttf`
  - Source: https://github.com/google/fonts/tree/main/ofl/delagothicone
  - Author: artakana
  - License: SIL Open Font License 1.1 (`Assets/_Project/Resources/Fonts/DelaGothicOne-OFL.txt`)
  - Use: Japanese display headings, score, and primary actions.
- Noto Sans JP Variable
  - File: `Assets/_Project/Resources/Fonts/NotoSansJP-VF.ttf`
  - Source: https://github.com/notofonts/noto-cjk
  - Authors: Adobe and Google
  - License: SIL Open Font License 1.1 (`Assets/_Project/Resources/Fonts/NotoSansJP-OFL.txt`)
  - Use: Japanese body copy, HUD labels, card details, and control guidance.
- Big Shoulders Display Black
  - File: `Assets/_Project/Resources/Fonts/BigShouldersDisplay-Black.ttf`
  - Source: https://github.com/xotypeco/big_shoulders
  - Author: Patric King / XO Type Co.
  - License: SIL Open Font License 1.1 (`Assets/_Project/Resources/Fonts/BigShoulders-OFL.txt`)
  - Use: retained from the earlier English UI; no longer selected by the runtime theme.
- IBM Plex Sans Condensed Medium
  - File: `Assets/_Project/Resources/Fonts/IBMPlexSansCondensed-Medium.ttf`
  - Source: https://github.com/google/fonts/tree/main/ofl/ibmplexsanscondensed
  - Author: IBM
  - License: SIL Open Font License 1.1 (`Assets/_Project/Resources/Fonts/IBMPlexSansCondensed-OFL.txt`)
  - Use: retained from the earlier English UI; no longer selected by the runtime theme.

The OFL permits commercial use, modification, and redistribution when its terms and reserved font-name requirements are retained. These font files are redistributed unmodified with their license texts.

## Project-generated visual asset

- File: `Assets/_Project/Resources/CharacterCardAtlas.png`
- Purpose: Lux and Bastion character-card illustrations and character-selection portraits.
- Source: generated specifically for this project with OpenAI's built-in image-generation tool on 2026-08-13.
- Prompt constraints: original characters, no text, no logo, no watermark, no copyrighted character reference.
- External attribution: none.

## Project-generated card art

- Files: `Assets/_Project/Resources/CardArt/*.png` (12 files, one for every card ID).
- Purpose: gameplay hand illustrations for all common, court, and character cards.
- Source: generated specifically for this project with OpenAI's built-in image-generation tool on 2026-08-21.
- Prompt constraints: original pop-punk tennis world, existing Lux/Bastion identity preserved for character cards, high contrast at small size, no text, no logo, no watermark.
- Prompt record: `Assets/_Project/Docs/CARD_ART_GENERATION_PROMPTS.md`.
- External attribution: none.

## Project-generated stadium art

- Files: `Assets/_Project/Resources/Stadium/FantasyStadiumMural.png`, `CrowdMosaic.png`, and `PunkBanner.png`.
- Purpose: far-background architecture, grandstand audience panels, and arena LED/banner graphics.
- Source: generated specifically for this project with OpenAI's built-in image-generation tool on 2026-08-21.
- Prompt constraints: original fantasy tennis stadium, graphical pop-punk style, existing navy/cyan/magenta/yellow palette, no text, no logo, no watermark.
- Prompt record: `Assets/_Project/Docs/STADIUM_ART_GENERATION_PROMPTS.md`.
- External attribution: none.

Before adding an asset, record its name, source URL, author, license, commercial-use terms, modification terms, redistribution terms, required attribution, and the files that use it.
