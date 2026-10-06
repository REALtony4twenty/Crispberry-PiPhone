# Crispberry PiPhone

An in-game phone for PEAK. Version 0.17.124.

Earlier releases stay up:

- [v0.17.81](https://github.com/REALtony4twenty/Crispberry-PiPhone/releases/tag/v0.17.81)
- [v0.17.69](https://github.com/REALtony4twenty/Crispberry-PiPhone/releases/tag/v0.17.69)

## Since 0.17.81

- The side volume buttons are the phone volume. That level is the master for button clicks, app hover, games, music, power on and off, and clips a mod plays through the phone. The shade slider is labeled Volume. Music keeps its own slider under that.
- Personalize, then Sounds, has a row for each interface sound: on or off, its own level, and which file it plays. Volume up and volume down follow the phone volume and are not on that page.
- New interface sounds: button, back, app hover, camera shutter, record start, record stop, slider, trash, toggle on, toggle off, vibrate, typing, and backspace. Games have their own instant-mix panel with a slider per effect.
- The shipped clips are leveled so a quiet tap and a loud beep sit near the same loudness.
- Leaving an app stops its sounds and its running work. Calls, messages, and music can keep going. Other apps do not.
- While a closed-phone alert is on screen, the walk-around key (Left Alt unless you changed it) unlocks the cursor so those buttons can be clicked. You can still move, look, and climb. Press the key again and the game takes the cursor back. That key does this only while the alert is up. With the phone open it is the usual walk steps.
- A cast of a built-in game draws that game's board for everyone in the room. Camera and Closet can show the world view. The phone can sit on the cast, or stay in your hand, and the app keeps running either way. The side volume buttons, the ringer key, and the front camera hole are not drawn on the cast. Another mod's app shows as the phone picture, so the UI that mod built is what people see.
- Four Across flashes the four that won.
- Mines is one timed board. The clock starts on the first safe reveal. The best time is kept.
- Toggles are switch icons. Their on and off colors can be chosen under Buttons.
- Sliders use a flat track. A start-and-end range is used where a clip has a start and an end.

## For other mods

Phone volume scales every sound below. A Sounds-page row, when that cue has one, is a trim under that volume.

- `PiPhoneApi.PlayInterfaceSound(cue)` plays a built-in clip. Cues include `click`, `toggle-on`, `toggle-off`, `back-btn`, `hover`, `tick`, `trash`, `shutter`, `rec-start`, `rec-stop`, `vibrate`, `type`, and `back`, plus the game cues.
- `PiPhoneApi.PlayClip(bytes)` plays your own short WAV or MP3 at the phone volume. The second argument is a 0–1 trim.
- `PiPhoneApi.BindButton(button, action)` plays the phone click, then runs the action. Pass a cue name, or use `BindButtonClip`, to play something else.
- `PiPhoneApp.HoverSound` is a built-in cue for that app's icon. `SetAppHoverClip` uses your own recording instead. `SetAppHoverSound` changes the cue later.
- `GetAppSfxVolume` / `SetAppSfxVolume` and `GetAppCueVolume` / `SetAppCueVolume` are the per-app and per-effect trims. `UiSfxVolume` is the fallback for an interface sound that has no row yet.
- `PiPhoneApp.RunInBackground` stays false unless the app must keep working after the player goes home.

Build buttons, toggles, and sliders with `PhoneUi` so they match this release and play the phone sounds:

- `CreateButton`, `CreateIconChip`, and `CreateToggleChip`
- `CreateSlider`, `CreateSliderRow`, and `CreateThinSlider`
- `CreateDualRange` for a start and end on one track

`CreateImage` does not take clicks. `SetClickable` or `CreateButton` is what makes a picture a button.

Cast, in addition to `RegisterCastDevice` and `SetCastAspect`:

- `MountPhone` / `UnmountPhone` put the real phone on a world socket.
- `SetPhoneHeld(false)` leaves the open phone on the cast. `SetPhoneHeld(true)` brings it back to the hand. The app stays open either way.
- A mod app does not send a separate board. The cast shows the phone picture.

## Since 0.17.69

- The phone starts off. Hold the side button to turn it on (HELLO). A short press while it is off shows the battery for 2 seconds. Holding the side button while it is on turns it off (GOODBYE). Those words follow the language setting.
- Toolbar icons fill the width, then wrap, in portrait and landscape.
- Alert Place toggles on and off without closing the phone.
- Screen cast lists airport flight boards that are in the scene. Other players in the room see the cast. Mods can register more screens, and an app can set the cast aspect. The picture still covers the whole board and is mirrored.
- Built-in games show as Stacker, Brick Break, Echo, Four Across, and Mines. Their saved app ids are unchanged.
- Mines Play mode counts boards cleared. Timed mode is one board and keeps the best time.
- Store pages scroll, and screenshots sit in a sideways gallery.
- `PiPhoneApp.Tooltip` is the icon hover text. If it is empty, the store description is used.
- Mods can set startup, power, and loading pages, open an app with data, add a status icon, snapshot and restore phone placement, and run a tick while the phone is closed.

Default open key is **Alt+P**. That key, and the camera and call keys, can be changed in the BepInEx config.

## Credits and licenses

The bitten crispberry logo (`Icons/logo.png`) are original work by tony4twenty and is not AI made.

### Material Symbols (Apache 2.0)

UI icons in `Icons/material`, except `shutter.png`, are rasterized from Google's Material Symbols (filled `fill1` and outline `default`, 48px). Rasterizing to PNG is the only change.

- https://github.com/google/material-design-icons
- https://fonts.google.com/icons
- License: `Icons/material/LICENSE` (Apache License 2.0)
- Notice: `Icons/material/NOTICE`

`shutter.png` is original art for this mod. Google does not publish that shutter glyph.

### Twemoji (CC BY 4.0)

Emoji pictures in `Icons/emoji` are Twemoji.

Twemoji is copyright 2020 Twitter, Inc and other contributors, licensed under CC BY 4.0.
https://creativecommons.org/licenses/by/4.0/
Graphics from https://github.com/jdecked/twemoji

### Sounds

Clips in `Sounds/` play on the phone. Two require attribution. The rest are CC0. The same list ships in the mod as `Sounds/CREDITS.txt`.

Bubble Pop by Jayvardhan — https://freesound.org/s/848515/ — CC BY 4.0
https://creativecommons.org/licenses/by/4.0/

Zweig11 by TenbuKan — https://freesound.org/s/250837/ — CC BY 3.0
https://creativecommons.org/licenses/by/3.0/

CC0, from Freesound (https://creativecommons.org/publicdomain/zero/1.0/):

- pop.aiff by sebtv — https://freesound.org/s/431592/
- pop from tube shaped like a candy cane by music_is_wiggly_air — https://freesound.org/s/812799/
- Duuwip by floomatic — https://freesound.org/s/826602/
- chip01 by SilverDubloons — https://freesound.org/s/817553/
- clatter10 by SilverDubloons — https://freesound.org/s/817567/
- tap 4.wav by Ayliffe — https://freesound.org/s/80100/
- Cartoon Jump.wav by farzyno — https://freesound.org/s/661297/
- A Very Clear Pop Sound by 9voltfan — https://freesound.org/s/784918/
- Startled Fall by Jerimee — https://freesound.org/s/527529/
- food.mp3 by bsp7176 — https://freesound.org/s/570636/
- move.mp3 by bsp7176 — https://freesound.org/s/570635/
- Retro Accomplished SFX by suntemple — https://freesound.org/s/253177/
- Select Sound by Tony_Cannoli — https://freesound.org/s/801081/
- Menu Selection SFX by mtndewfan123 — https://freesound.org/s/687451/
- Click menu by SLV443 — https://freesound.org/s/733769/
- Pencil dropping.wav by radioscoopfi — https://freesound.org/s/607073/
- card.mp3 by Johnny2810 — https://freesound.org/s/559531/
- Congrats by Fupicat — https://freesound.org/s/607207/
- Short Glitch by unfa — https://freesound.org/s/249705/
- Wrong Answer by Beetlemuse — https://freesound.org/s/528956/
- swoop2g.flac by Streety — https://freesound.org/s/30241/
- Bucket 0130 by Dreadwolf910 — https://freesound.org/s/825127/
- Trung Gach Da by SieuAmThanh — https://freesound.org/s/530812/
- Block_condensor.wav by billtipp — https://freesound.org/s/523456/
- Digging in wet course sand by f3bbbo — https://freesound.org/s/651292/
- Retro Game SFX Explosion by suntemple — https://freesound.org/s/253169/
- coral4.ogg by cuanmamang — https://freesound.org/s/703929/
- Cell phone buzz once by fitzysfunhouse — https://freesound.org/s/540215/
- Pop Sound by Tony_Cannoli — https://freesound.org/s/858836/
- Jump_C_04 by cabled_mess — https://freesound.org/s/350906/
- Hover 1 by plasterbrain — https://freesound.org/s/237422/
- Menu Selection Click by NenadSimic — https://freesound.org/s/171697/
- beep_up.wav by paep3nguin — https://freesound.org/s/388046/
- beep_down.wav by paep3nguin — https://freesound.org/s/388047/
- button-selected.wav by StavSounds — https://freesound.org/s/546079/
- button-pressed.wav by StavSounds — https://freesound.org/s/546078/
- For The Camera Perc by adh.dreaming — https://freesound.org/s/668366/
- Glass Tap by Unicornaphobist — https://freesound.org/s/262958/
- ui_tap.wav by LilMati — https://freesound.org/s/657948/
- click.wav by Poiqz — https://freesound.org/s/394789/
- Toilet lid close by jimbo555 — https://freesound.org/s/630491/
- lightswitch.wav by j1987 — https://freesound.org/s/335745/
- LIGHT SWITCH 018 by leonelmail — https://freesound.org/s/508071/

A snake game-over clip at https://freesound.org/s/570633/ is CC BY-NC and is not included. The snake loss sound is generated in the mod.

### Kenney playing cards (CC0)

Solitaire cards in `Icons/cards` are from Kenney's Playing Cards Pack, released under CC0 1.0.
https://kenney.nl/assets/playing-cards-pack
https://creativecommons.org/publicdomain/zero/1.0/
