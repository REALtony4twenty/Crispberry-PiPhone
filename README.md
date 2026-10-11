# Crispberry PiPhone

An in-game phone for PEAK. Version 0.17.150.

## Install

1. Install BepInEx for PEAK.
2. Download `CrispberryPiphone.dll` from the latest GitHub release.
3. Put it anywhere under `PEAK/BepInEx/plugins`. Remove older copies of the PiPhone DLL so only one loads.
4. Restart PEAK. Everyone using calls, messages, shared casts, nearby phone audio, or synchronized flashlights should use the same version.

Press **Alt+P** to open or close the phone, or use the PiPhone button in PEAK's pause menu. Hold the **left ringer key** on the phone bezel for about half a second to turn the phone on or off; tap it while the phone is off to check the battery. A tap while it is on cycles Ringer, Vibrate, and Silent.

## Main controls

- **Alt+P:** open or close the phone.
- **F1 / F2:** answer or decline a call; open or dismiss a text alert.
- **F3:** rotate the open phone.
- **F4:** move the open phone onto the selected cast screen or return it to your hand.
- **Left Alt while open:** switch between looking around, walking with the cursor, and phone-only control.
- **Right-click outside the phone:** close it. Left-clicking outside does not close it.
- **Camera:** F flips front/back, R switches tall/wide, V takes a photo or starts/stops video, and the wheel zooms.
- Pull down the status bar for brightness, master/channel volume, phone-position lock, size, Do Not Disturb, ringer, flashlight, rotation, navigation bar, alert placement, screen cast, Settings, recent notices, and Audio controls while music is playing. Right-click the flashlight for its color menu. **Settings → Personalize → Toolbar** chooses which tiles appear.
- Open **Settings → Controls** to change phone, alert, camera, music, and game keys.
- The phone cannot be opened while your scout is unconscious.

## Apps and features

### Communication

- **Phone:** call one scout or a group in the current lobby, manage contacts and recents, set custom contact names/photos/tones, and use video calls. The speaker button lets nearby players hear the other caller. Dialing 911, 112, 999, or 4357 calls the Scoutmaster.
- **Messages:** send text, photos, GIFs, voice messages, emojis, and stickers to scouts in the lobby. Type an emoji shortcut such as `:backpack:` or use the emoji and sticker menus. Stickers remain in the conversation instead of being copied into Gallery Files. Sending videos and arbitrary files is not supported yet. GIF search works without setup; modpack makers can provide a BepInEx `TenorApiKey`.
- **Voicemail:** missed callers can leave recordings that can be played later.
- Calls and messages show actionable alerts even when the phone is closed. The alert can also control music. Alert placement, Do Not Disturb, ringer mode, and app notification permissions are configurable.

### Camera, photos, and appearance

- **Camera:** take photos or record videos with front/back cameras, portrait/wide framing, zoom, emotes, and a white camera flash. Selfie photos also flash the phone screen.
- **Photos:** browse the Photos, Videos, and Files tabs; view or delete media and use a photo, GIF, or WebP as wallpaper. Photos is private on shared casts.
- **Closet:** change the scout's appearance from the phone. It also supports compatible More Customizations outfits and SkinColorSliders.
- The shade flashlight is visible to everyone, follows where the scout looks, and has brightness, focus, white, warm, cool, skin-color, and custom-color options.

### Audio and tools

- **Audio:** import songs, build playlists, and control music from the app, shade, or closed-phone alert. Master, Media, Call, Ringtone, Notification, and System volumes are separate.
- **Voice Memos:** record, save, play, and delete recordings.
- **MakeNoti:** import or trim audio into ringtone, text-tone, or notification clips. Clips can also be placed in `BepInEx/config/CrispberryPiPhone/alerts`.
- **Notes:** keep local notes and lists.
- **Apps:** install or remove optional built-in apps and discover apps registered by other mods. Phone, Messages, Settings, and Apps itself are permanent.
- **Settings:** customize colors, filled/outline icons, language, clock, side buttons, ringer, phone size and position, brightness, dock, navigation bar, wallpaper, interface sounds, emoji/sticker packs, alerts, and controls. English, Spanish, and French are built in; mods can add more languages.
- Long-press a home icon to move it to or from the dock/home screen or uninstall it when allowed. The navigation bar's apps button opens **All apps**. Up to four apps fit in the dock.

### Games

Snake, Four Across, Sudoku, and draw-1 Solitaire are preinstalled. Install 2048, Mines, Echo, Stacker, and Brick Break from **Apps**. Every game has its own controls and effect-volume menu; game keys can be changed in **Settings → Controls**. Four Across supports another player or the CPU.

### Screen cast and nearby sound

- Use the cast chip to select an airport flight board or a screen registered by another mod. Only one player can cast in a room at a time.
- F4 can leave the real open phone on the screen as a touch screen. Other players still receive the shared cast.
- Shared casts rebuild normal phone UI sharply, send still pictures once, and automatically use adaptive video for changing or undescribable areas. Weak connections step down in quality.
- Private apps and every screen shown during a call display a padlock to watchers. Phone sound comes from the cast screen while casting; otherwise nearby players hear it from the scout with distance falloff.

## Saved files

Phone data is kept under `BepInEx/config/CrispberryPiPhone`. This includes pictures, videos, downloads, songs, alerts, messages, call history, voice memos, playlists, layout, and theme settings. Back up that folder to preserve the phone's local content.

Earlier releases stay up:

- [v0.17.124](https://github.com/REALtony4twenty/Crispberry-PiPhone/releases/tag/v0.17.124)
- [v0.17.81](https://github.com/REALtony4twenty/Crispberry-PiPhone/releases/tag/v0.17.81)
- [v0.17.69](https://github.com/REALtony4twenty/Crispberry-PiPhone/releases/tag/v0.17.69)

## Emoji packs

A folder named `Emojis` anywhere under `BepInEx/plugins` adds its pictures to Messages. `emojis` and `EmOjiS` count too. `.png`, `.jpg`, and `.jpeg` files count. A picture bigger than 128 on either side is skipped. A picture that is exactly 128 by 128 is an emoji and a sticker. Anything smaller is an emoji only. The file name is the shortcut, so `PEAK_Point.png` is typed `:PEAK_Point:` and also shows in the emoji menu. Stickers are grouped by the mod that added them. Personalize, then Emojis, has an emoji switch and a sticker switch for each pack. Message emoji pictures grow with the font size. Stickers have their own menu in Messages, and tapping one sends it immediately as its own message at its own shape. A sticker stays in the message and is not added to Files. One `Emojis` folder can hold both. Hold a picture in that menu to favorite it. Restart PEAK after adding or changing pictures.

Personalize, then Emojis, lists Crispberry, PEAK, and every pack. Turn a set off and its pictures stay out of the menu. Turn Crispberry off when a pack should replace the built-in set. PEAK is the game's own pictures: item icons, badges, hats, outfits, status icons, emotes, and a few interface icons. They are read from the game's files, the same hats and outfits the closet uses and the same emotes the camera uses, so they show up without starting a climb. Items stay on the PEAK tab. Badges, hats, outfits, emotes, status marks, and interface icons each have their own tab. The shortcut is the picture name in lowercase, so the backpack is `:backpack:`. The 128 size rule is for mod folders. PEAK pictures keep their real size.

The phone makes one menu tab per pack, named from the mod folder. A Thunderstore folder `AtomicStudio-PEAK_Chat_Emojis` shows as **PEAK Chat Emojis** and holds every picture from that pack. A loose `BepInEx/plugins/Emojis` folder shows as **Custom**. Pack authors do not need a plugin or a phone API. PeakTextChat reads the same pictures and ignores the json below, so one folder can feed both.

To also file pictures into Smileys, Food, and the other built-in tabs, put a `.json` file in that same `Emojis` folder. One category per object. Use a list when you need more than one. The shortcut stays the file name.

```json
[
  { "category": "food", "emojis": "apple, burger" },
  { "category": "smile", "emojis": "Scout" }
]
```

`emojis` can be that comma-separated text, or a list of names. Category words: `smile`, `people`, `nature`, `food`, `travel`, `play`, `things`, `signs`, `flags`. Any other word becomes its own tab.

## Since 0.17.124

### Audio and screen cast integration by endors-toi

- The audio manager from endors-toi's work is fully integrated. Every built-in sound is routed through Media, Call, Ringtone, Notification, or System, with a saved slider for each channel under Master.
- Alerts duck Media. Calls can pause or silence Media and restore it at hang-up. The public API can play clips, full media, AudioClips, or live PCM streams and read or set channel volumes.
- Shared cast was rebuilt around a sharp description of the UI, one-time still-picture transfers, automatic video fallback for moving or undescribable content, per-app video controls, video regions, independent audio, adaptive quality, and protection against bad peers and picture swaps.
- Shared cast audio comes from the cast screen. With no watcher, nearby players still hear the phone from its owner with distance falloff. Only one cast can own the room at a time.

### Emojis, stickers, and messages

- Messages finds every case-insensitive `Emojis` folder under `BepInEx/plugins` and automatically groups its pictures by mod. Optional json files add category tabs.
- Mod pictures over 128 pixels on either side are skipped. Exactly 128×128 pictures are both emojis and stickers; smaller pictures are emoji-only. Every pack has separate emoji and sticker switches.
- PEAK's item, badge, hat, outfit, status, emote, and interface pictures are available as full-size emojis and stickers. Their lowercase picture names are shortcuts, such as `:backpack:`.
- Emoji pictures in bubbles and the picker scale with the phone font setting. Stickers send at their own shape, display directly in the thread, and no longer get copied into Gallery Files.

### Flashlight, camera, calls, and controls

- The synchronized shade flashlight starts off each launch. It has draggable brightness and focus sliders, a fading visible cone, and white, warm, cool, skin, or custom colors. Closing the phone leaves it on for that session.
- Camera has a separate always-white flash for rear photos/videos and a dimmer selfie light. A front-camera photo also turns the phone screen white briefly.
- People standing nearby hear ringtones, notifications, voice memos, voice messages, music, and interface sounds. Speakerphone adds the remote caller to that nearby sound.
- Emergency numbers 911, 112, 999, and 4357 ring the Scoutmaster. Closing the phone during ringing no longer strands the call. He answers on video, opens the phone, spawns about 80 meters away and 5 meters above terrain, waits 4 seconds, then sprints. The call can be ended while ringing, closes on a grab, and ends after a minute if he has not caught the player. Ghosts cannot request rescue; in multiplayer the master client also needs PiPhone.
- Dial-pad keys play only their DTMF tones, and outgoing calls have ringback. The phone remains usable during an active call.
- Sliders ignore keyboard movement keys and support normal dragging. Camera zoom remains on the wheel and on-screen controls.
- English, Spanish, and French phone chrome are built in. Other mods can register a language pack or provide a `lang` key/value file.
- Theme changes correctly refresh an open shade without interrupting the flashlight color picker.

## Since 0.17.81

This is the player-facing list. Where an older note below disagrees, this section is the current behavior.

### Power, closing, and the cursor

- Hold the left ringer key to turn the phone on. The screen says HELLO and plays a short chime. Hold it again to turn the phone off. The screen says GOODBYE and plays a lower chime. A short press while the phone is off shows the battery for 2 seconds. HELLO and GOODBYE follow the language setting. The screens were in 0.17.81. The chimes are new.
- Left-clicking the dark area around the phone does not close it. Right-click that area to close it. The open/close key still works.
- While a closed-phone alert is on screen, the walk-around key (Left Alt unless you changed it) unlocks the cursor so you can click the alert buttons. You can still move, look, and climb. Press the key again and the game takes the cursor back. That only happens while the alert is up. With the phone open, the same key is the usual walk steps: look around, then walk with the cursor, then phone only.
- F4 (unless you changed it) puts the open phone on the cast, or brings it back to your hand. The app stays open either way.

### Volume and sounds

- The side volume buttons, and the shade slider labeled Master, are the phone volume. That level is the master for button clicks, app hover, games, music, the power chimes, and clips a mod plays through the phone. Pressing volume up plays the up sound louder. Pressing volume down plays the down sound quieter.
- The arrow next to Master opens five more sliders: Media, Call, Ringtone, Notification, and System. Each one sits under the master. Media is music, games, videos, voice memos, and sound from other mods. Call is the other scouts' voices on a call. System is the interface sounds. Music keeps its own slider in the Audio app, under Media. Vibrate follows only its own row on the Sounds page.
- A ringtone, an alert, or a vibrate buzz lowers Media while it sounds. Fade music on alerts, on the Notifications page, turns that off.
- Prioritize call audio, on the Notifications page, is on by default. Media is lowered while a call rings in. While a call is connected, the Audio app pauses and other media is silent. Both come back at hang-up.
- Personalize, then Sounds, has a row for each interface sound. Each row can be turned off, turned down, or pointed at any other phone sound. The rows are Button, Toggle off, Toggle on, Back, App hover, Camera, Record start, Record stop, Slider, Trash, Vibrate, Typing, and Backspace. Volume up and volume down follow the phone volume and are not on that page. The power chimes are not on that page either.
- The shipped clips were leveled so a quiet tap and a loud beep sit near the same loudness.
- Notification and ringtone pickers show which sound is in use.
- Almost every phone clip can also be chosen as an alert, a ringtone, or a button sound.
- Leaving an app stops its sounds and the work it started. Calls, messages, and the Audio player can keep going. Games and the other built-in apps do not.

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
- Photos, Messages, the dialer, voicemail, notes, and voice memos stay private on a shared cast. Watchers see a padlock instead.
- Players watching a shared cast see a rebuilt copy of the phone, so text and shapes stay sharp. A still picture is sent to each watcher once. A picture that keeps changing, such as a game another mod draws, is sent as video. Watchers see another mod's app whether or not they have it installed.
- While you cast, the phone's sound comes from the cast screen instead of from the phone. Players watching a shared cast hear it from that screen too. If nobody is watching, other players still hear the phone from where you are standing, quieter as they get farther away. Sound and video quality step down for a watcher on a weak connection, and back up when it clears.
- Only one player in the room can cast at a time.
- You can still cast to airport flight boards that are in the scene, and mods can still register more screens.

## For other mods

Phone volume scales every sound below, and `PiPhoneApi.MasterVolume` reads and sets it. Each sound plays on a channel with its own volume under that master. A Sounds-page row, when that cue has one, is a trim under both.

App and phone integration:

- `RegisterApp` adds an app. `Install`, `Uninstall`, `IsInstalled`, `GetInstalledApps`, `AddToHome`, and `RegisterCategory` manage Apps/home integration. `PiPhoneApp` controls preinstallation, store listing, stickiness, background work, play-through, cast privacy, and cast video.
- `OpenApp(id, payload)`, `GoHome`, `GoBack`, `SetImmersive`, `SetPlayThrough`, and the orientation events control presentation.
- `RegisterKeybind`, `KeyDown`, `KeyHeld`, `CaptureKeybind`, `GetKeybinds`, and `FormatKey` integrate controls.
- `RegisterShadeButton`, `RegisterNavButton`, and their visibility/removal helpers extend phone chrome. `RegisterNumber` adds a dialable number.
- `Notify` posts an in-phone notification. `AlertsFolder`, `ImportAlert`, and app-alert sound accessors integrate custom tones.
- Contact, scout, and voice-filter helpers expose saved contacts, current scouts, and call voice effects.
- `RegisterLanguage`, `SetLanguage`, `GetLanguages`, and `T` add translated phone text.
- Power/stage, battery/carrier, wallpaper, look, flashlight, placement, snapshot/restore, and mount APIs are also public. See the XML documentation in `PiPhoneApi.cs` for signatures.

- `PiPhoneApi.PlayInterfaceSound(cue)` plays an interface cue on the System channel. The cues are `click`, `toggle-on`, `toggle-off`, `back-btn`, `hover`, `tick`, `trash`, `shutter`, `rec-start`, `rec-stop`, `type`, and `back`. Any other name plays nothing: alert tones, `vibrate`, and the game cues cannot be played by name.
- `PiPhoneApi.PlayClip(bytes)` plays your own short WAV or MP3 on the Media channel, so it follows the Media slider and the phone volume. The second argument is a 0–1 trim.
- A ringtone, an alert or a vibrate buzz lowers everything on the Media channel while it sounds, your sound included, unless the player turns off Fade music on alerts. With `PiPhoneApi.PrioritizeCallAudio` on, which is the default, Media is also lowered while a call rings in and silent, but still running, while a call is connected.
- `PiPhoneApi.BindButton(button, action)` plays the phone click, then runs the action. Pass an interface cue, or use `BindButtonClip`, to play something else. A name that is not an interface cue runs the action with no sound. Presses play on the System channel and keep the player's Button row.
- `PiPhoneApp.HoverSound` is an interface cue for that app's icon. `SetAppHoverClip` uses your own recording instead. `SetAppHoverSound` changes the cue later. Either way the hover plays on the System channel, and the player's App hover switch and volume still apply.
- `GetAppSfxVolume` / `SetAppSfxVolume` and `GetAppCueVolume` / `SetAppCueVolume` are the per-app and per-effect trims. `UiSfxVolume` is the fallback for an interface sound that has no row yet.
- `PiPhoneApp.RunInBackground` stays false unless the app must keep working after the player goes home.

Longer audio goes through the phone on the Media channel:

- `PiPhoneApi.PlayMedia(bytes, rate, channels, loop)` plays a whole WAV file, or raw 16-bit PCM at the rate and channel count you pass. `PlayMediaClip(clip, loop)` plays an AudioClip you already hold.
- `PiPhoneApi.PlayMediaStream(reader)` is for sound your mod makes as it goes, such as a game or an emulator. The phone calls `reader` on the audio thread to fill its output buffer. Write samples at full level. The phone applies the volume.
- `PiPhoneApi.StopMedia()` stops all three and leaves the Audio app alone. `IsMediaPlaying()` is true while anything is on Media, the Audio app included.
- `GetChannelVolume` / `SetChannelVolume` read and save one `PiPhoneAudioChannel`. A mod can set every channel, but its own sound only plays on Media.
- Anything played through the phone is heard from the cast screen while the player casts, with nothing extra to do.

Build buttons, toggles, sliders, images, scroll rows, and icon controls with `PhoneUi` so they match this release and play the phone sounds:

- `CreateButton`, `CreateIconChip`, and `CreateToggleChip`
- `CreateSlider`, `CreateSliderRow`, and `CreateThinSlider`
- `CreateDualRange` for a start and end on one track
- `CreateHorizontalScroll`, `CreateImage`, `FitContained`, `SetClickable`, and `SetTooltip`

`CreateImage` does not take clicks. `SetClickable` or `CreateButton` is what makes a picture a button.

Cast, in addition to `RegisterCastDevice` and `SetCastAspect`:

- `MountPhone` / `UnmountPhone` put the real phone on a world socket.
- `SetPhoneHeld(false)` leaves the open phone on the cast. `SetPhoneHeld(true)` brings it back to the hand. The app stays open either way.
- A mod app does not send a separate board. Watchers get a rebuilt copy of your screen, and your own pictures are sent to them.
- `PiPhoneApp.CastHidden` keeps your app off casts. Watchers see a padlock and hear nothing from it.
- `PiPhoneApp.CastVideo` is `Auto`, `On`, or `Off`. `Auto` sends video only for a picture that keeps being repainted, or a screen the phone cannot describe. `On` always sends the app area as video. `Off` never does.
- `PhoneUi.CreateVideoCastContainer(parent, name)` is a box for a picture you keep repainting. Only that box is sent as video, and the rest of your screen stays sharp. Use one per screen.

## Since 0.17.69

- The phone starts off. Hold the left ringer key to turn it on (HELLO). A short press while it is off shows the battery for 2 seconds. Holding the ringer key while it is on turns it off (GOODBYE). Those words follow the language setting.
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
