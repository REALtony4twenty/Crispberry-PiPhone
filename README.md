# Crispberry PiPhone

An in-game phone for PEAK. Version 0.17.124.

Earlier releases stay up:

- [v0.17.81](https://github.com/REALtony4twenty/Crispberry-PiPhone/releases/tag/v0.17.81)
- [v0.17.69](https://github.com/REALtony4twenty/Crispberry-PiPhone/releases/tag/v0.17.69)

## Since 0.17.81

This is the player-facing list for 0.17.124. Where an older note below disagrees, this section is the current behavior.

### Power, closing, and the cursor

- Hold the side button to turn the phone on. The screen says HELLO and plays a short chime. Hold it again to turn the phone off. The screen says GOODBYE and plays a lower chime. A short press while the phone is off shows the battery for 2 seconds. HELLO and GOODBYE follow the language setting. The screens were in 0.17.81. The chimes are new.
- Left-clicking the dark area around the phone does not close it. Right-click that area to close it. The open/close key still works.
- While a closed-phone alert is on screen, the walk-around key (Left Alt unless you changed it) unlocks the cursor so you can click the alert buttons. You can still move, look, and climb. Press the key again and the game takes the cursor back. That only happens while the alert is up. With the phone open, the same key is the usual walk steps: look around, then walk with the cursor, then phone only.
- F4 (unless you changed it) puts the open phone on the cast, or brings it back to your hand. The app stays open either way.

### Volume and sounds

- The side volume buttons, and the shade slider now labeled Volume, are the phone volume. That level is the master for button clicks, app hover, games, music, the power chimes, and clips a mod plays through the phone. Pressing volume up plays the up sound louder. Pressing volume down plays the down sound quieter. Music keeps its own slider under that master.
- Personalize, then Sounds, has a row for each interface sound. Each row can be turned off, turned down, or pointed at any other phone sound. The rows are Button, Toggle off, Toggle on, Back, App hover, Camera, Record start, Record stop, Slider, Trash, Vibrate, Typing, and Backspace. Volume up and volume down follow the phone volume and are not on that page. The power chimes are not on that page either.
- The shipped clips were leveled so a quiet tap and a loud beep sit near the same loudness.
- Notification and ringtone pickers show which sound is in use.
- Almost every phone clip can also be chosen as an alert, a ringtone, or a button sound.
- Leaving an app stops its sounds and the work it started. Calls, messages, and the music player can keep going. Games and the other built-in apps do not.

### Look, toolbar, and settings

- In portrait, the quick-settings toolbar fits 9 icons across, then wraps the 10th onto the next line. The icons stayed the same size. The gap between them got smaller so the ninth one fits.
- Toggles are switch icons, tinted with the on color and the off color. Those two colors are chosen on the Buttons page. Resetting Look or Buttons does not reset them.
- Settings opens into Controls and Personalize. Personalize holds Style, Language, Clock, Buttons, Display, Home screen, Sounds, and Notifications.
- Display has a live preview of vertical and landscape, the size slider, brightness, and a reset for the phone's position. You can still drag the phone by its rim.
- Home screen has the dock, the navigation bar, and Wallpaper. Wallpaper can be the default background or a photo or GIF from Photos.
- Style colors include the clock, the battery, and the signal, along with the rest of the look. The color picker is a field and a hue bar, and each color has its own reset.
- Clock options stay as words next to their switches: 24-hour or AM/PM, PEAK time or real-world time, and whether the date is hidden.
- Place closed-phone alerts is on the Controls page, with a reset that puts the banner back at its start. The banner itself looks the same.
- MakeNoti uses one editor. In portrait the start/end range is vertical. In landscape it is horizontal. The file line names the kind of clip. Preview, save, and the ringtone/alert switch sit at the bottom.
- Sliders use a flat track and play a tick while you drag. A start-and-end range is the control used when a clip has a start and an end.

### Games

Snake, Stacker, Brick Break, Echo, 2048, Mines, Sudoku, and Solitaire open straight into play. Their old front menus are gone. Restart is the New button on the game bar, next to exit. Four Across still has its lobby. Each of those games has a Sound button (the instant-mix icon) with one volume slider per effect, not one slider for the whole game.

Landscape boards sit lower on the phone, closer to the navigation, and they are taller than in 0.17.81. The score stays on the left and the controls stay on the right, on screen, instead of piled on the outer edge. Portrait layout for those boards is the same size it was.

- Snake food is the Crispberry PiPhone logo. Filling the board wins. Turning, eating, and winning have their own sounds. Hitting the wall or yourself is still a short tone.
- Stacker's next piece sits at the top-right of the playfield.
- Brick Break, Echo, 2048, Sudoku, and Solitaire use the same play bar. Echo plays a short song on a win and on a loss. Solitaire cards slide. Sudoku starts on Easy, and Easy / Medium / Hard stay as words. Solitaire is still draw-1. Kings still only go on empty columns. Aces still start the foundations.
- Mines is one timed board. There is no separate Play mode that counts boards. The clock starts on the first safe reveal, and that first tap is safe. The best time is kept.
- Four Across plays a falling disc. When four connect, those four flash so the win is easy to see, including on a cast.

### Messages, camera, photos, and the store

- Messages rebuilds when you rotate the phone, and keeps the open thread. Bubbles use the wider landscape width.
- The camera plays a shutter sound for a photo, a start sound when recording begins, and a stop sound when recording ends.
- App Store tiles, screenshots, and the install row play the button click. A screenshot opens large, and another tap closes it. App names under the icons can wrap.

### Screen cast

- Built-in games draw their board on the cast for everyone in the room. That replaced sending a photo of the phone for those games.
- Camera and Closet can show the world view.
- The phone can sit on the cast, or stay in your hand. F4 switches that. The app keeps running either way.
- The side volume buttons, the ringer key, and the front camera hole are not drawn on the cast.
- Messages, the dialer, voicemail, notes, and voice memos stay private on a shared cast.
- Another mod's app shows as the phone picture, so the UI that mod built is what people see.
- You can still cast to airport flight boards that are in the scene, and mods can still register more screens.

## For other mods

Phone volume scales every sound below, and `PiPhoneApi.MasterVolume` reads and sets it. Each sound plays on a channel with its own volume under that master. A Sounds-page row, when that cue has one, is a trim under both.

- `PiPhoneApi.PlayInterfaceSound(cue)` plays an interface cue on the System channel. The cues are `click`, `toggle-on`, `toggle-off`, `back-btn`, `hover`, `tick`, `trash`, `shutter`, `rec-start`, `rec-stop`, `type`, and `back`. Any other name plays nothing: alert tones, `vibrate`, and the game cues cannot be played by name.
- `PiPhoneApi.PlayClip(bytes)` plays your own short WAV or MP3 on the Media channel, so it follows the Media slider and the phone volume. The second argument is a 0–1 trim.
- A ringtone, an alert or a vibrate buzz lowers everything on the Media channel while it sounds, your sound included, unless the player turns off Fade music on alerts. With `PiPhoneApi.PrioritizeCallAudio` on, which is the default, Media is also lowered while a call rings in and silent, but still running, while a call is connected.
- `PiPhoneApi.BindButton(button, action)` plays the phone click, then runs the action. Pass an interface cue, or use `BindButtonClip`, to play something else. A name that is not an interface cue runs the action with no sound. Presses play on the System channel and keep the player's Button row.
- `PiPhoneApp.HoverSound` is an interface cue for that app's icon. `SetAppHoverClip` uses your own recording instead. `SetAppHoverSound` changes the cue later. Either way the hover plays on the System channel, and the player's App hover switch and volume still apply.
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
- Screen cast lists airport flight boards that are in the scene. Other players in the room see the cast. Mods can register more screens, and an app can set the cast aspect. 0.17.124 draws built-in game boards instead of a photo of the phone. See the cast section above.
- Built-in games show as Stacker, Brick Break, Echo, Four Across, and Mines. Their saved app ids are unchanged.
- 0.17.81 had a Mines Play mode that counted boards, plus a Timed mode. 0.17.124 is one timed board. The best time is kept.
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
