# OK Régis

**English** · [Français](README.fr.md)

*Vibe coded Voice Command OBS Manager.*

> A **voice assistant for OBS** on Windows. Talk to it like a real control room — *"Ok Régis, switch to Gameplay"*, *"Régis, hide the chat"*, *"mute Discord"* — and it does it in OBS and answers with a voice.

- 🎙️ **100 % local**: speech recognition runs on your PC (Vosk). No cloud, no account, no subscription.
- 🌍 **French, English and Spanish**: interface *and* voice commands.
- 🪶 **Very light**: ~15–40 MB of working memory and ≈ 0.3 % of one CPU core while listening (details below).
- 🔌 **Zero babysitting**: starts with Windows, sleeps in the tray, wakes up when OBS opens and goes back to sleep when OBS closes.
- 🛡️ **Safe for your stream**: it is a separate app, **not an OBS plugin** — if it ever crashes, OBS keeps running.

## Download

Get the latest installer from the [**Releases**](../../releases) page: `OKRegis-Setup-x.y.z.exe`.
It installs for your user (no administrator rights) and **includes the voice models for all three languages** — nothing else to download.

## First launch

1. Choose your language (French / English / Español).
2. In OBS enable the WebSocket server: **Tools → WebSocket Server Settings → Enable WebSocket server** (OBS 28 or newer). Note the port (default `4455`) and the password.
3. In OK Régis, pick a name for your assistant (default: *Régis*), your microphone, and enter the OBS port/password. Click **Test the OBS connection**.
4. Done. OK Régis now lives in the system tray. Open OBS and it starts listening within ~3 seconds.

## Voice commands

General form: **`[ok|hey] <assistant name> <command>`** — for example *"ok Régis switch to Gameplay"*.

| | English | Français | Español |
|---|---|---|---|
| Switch scene | *switch to / go to / scene* **Gameplay** | *mets / passe sur / scène* **Gameplay** | *cambia a / pasa a / escena* **Gameplay** |
| Show a source (in the shown scene) | *show / display / enable* **webcam** | *affiche / montre* **webcam** | *muestra / enciende / activa* **webcam** |
| Hide a source | *hide / remove / disable* **webcam** | *cache / masque / enlève* **webcam** | *oculta / esconde / quita / apaga* **webcam** |
| Mute a sound | *mute* **discord** · *mute the mic* | *coupe / mute* **discord** · *coupe le micro* | *silencia / mute* **discord** · *silencia el micro* |
| Unmute | *unmute* **discord** | *remets / unmute* **discord** | *reactiva / unmute* **discord** |
| Volume ± 3 dB | *raise / lower* **spotify** | *monte / baisse* **spotify** | *sube / baja* **spotify** |
| Recording | *start / stop / pause recording* | *lance / arrête / pause l'enregistrement* | *graba · para de grabar · pausa la grabadora* |
| Stream *(off by default)* | *start / stop streaming* | *lance / arrête le stream* | *inicia / para el stream* |

Notes:
- **The verb decides what you mean.** *Switch to* only looks at **scenes**; *show/hide* only looks at the **sources of the scene currently shown**; *mute/volume* only look at **sounds**. A scene and an overlay can share a name without any confusion.
- Names are matched loosely (case, accents and punctuation ignored, small typos tolerated), and a single word of a name is enough (*"chatting"* for *"Just Chatting"*).
- If Régis is not sure, **it does nothing** and writes it in the history.
- Every command type can be switched off in the settings. **Stream commands are off by default.**
- While Régis speaks, the microphone is muted so it never triggers on its own voice.

## The window

Open it from the tray icon. Header: state, last phrase heard, **Pause**, **Refresh**, **Settings**, and a **☀/☾ button to switch between light and dark themes**.

- **Scenes** — every scene like in OBS, with its sources underneath (click a scene to expand). Give any name a **nickname**: click **+ Nickname**, type a word, press Enter. Names the voice model cannot hear are flagged with ⚠ — a nickname fixes it. **Drag the ⠿ handle to reorder** scenes or sources (the order is kept by the app; OBS itself is not changed).
- **Sounds** — your audio sources with a one-click **On / Muted** button and nicknames.
- **Régis's replies** — for each action choose the phrase Régis says (`{scene}` / `{source}` are replaced by the name) and/or **your own sound file** (wav/mp3, played instead of the voice). ▶ lets you preview. Or pick *just a beep* or *silence*.
- **History** — everything heard and what was done with it.

## Settings

Language · assistant name · appearance · microphone with a **boost and a live level meter** (for quiet microphones — the boost only affects what Régis hears, never OBS) · reply output device (so you decide whether the voice reaches the stream) · OBS address/port/password · volume step · confidence threshold · which commands are enabled · *save CPU* mode · start with Windows.

Changing the **language** restarts the app (a different voice model is loaded). Settings are stored in `%AppData%\OK-Regis\settings.json`; the OBS password is encrypted with Windows DPAPI.

## Performance

Measured on a real setup with OBS open and a large scene collection:

| | Value |
|---|---|
| Working memory (Task Manager) | **~15 MB** idle, **~40 MB** after a few seconds of listening |
| CPU while listening | **≈ 0.3 %** of one core |
| CPU when OBS is closed | ≈ 0 % (the microphone and the voice model are released) |

The window itself is drawn on a **single surface** (no control per row), with **60 FPS animations** — smooth scrolling, expanding scenes, dragging cards, switching theme — even with hundreds of sources. Animations only run while they last; at rest nothing runs.

How: the speech engine only runs while you talk (a small voice-activity gate), the window does not exist in memory until you open it, unused memory is handed back to Windows after a few seconds of inactivity, and the recognition grammar only contains the valid sentences for your current scenes (which is also what makes it accurate).
The voice model itself reserves ~390 MB of *committed* memory; Windows only keeps the parts it actually needs in RAM.

## Troubleshooting

- **Régis does not react / reacts rarely** — open the settings and speak: the **microphone level meter** should reach the green zone. If it says *too quiet*, raise the **microphone boost** (this changes only what Régis hears, not your OBS audio). Also check the **History** tab: *"Not sure enough"* → lower the *confidence threshold*. Still nothing → untick **Save CPU**.
- **⚠ "Régis can't hear this name"** — the voice model does not know that word (e.g. `Texte (GDI+) 2`). Add a nickname with a common word.
- **Cannot connect to OBS** — OBS must be open with the WebSocket server enabled; check port and password.
- **The assistant name shows a warning** — it is unknown to the voice model or too close to a command word. Try another spelling or name.

## Build from source

Requires the .NET 10 SDK on Windows.

```powershell
dotnet test tests/OkRegis.Tests                       # unit tests
powershell -ExecutionPolicy Bypass -File publish.ps1  # self-contained build in .\dist
```

The first run downloads the voice model of the chosen language (≈ 40 MB, from alphacephei.com) unless a `models` folder sits next to the executable. The installer script is in [`installer/`](installer/).

```
src/OkRegis.Core   all logic without hardware: parsing, grammar, languages, orchestration (unit tested)
src/OkRegis.App    the Windows app: OBS WebSocket, microphone (NAudio + Vosk), voice, tray, windows
tests/             xUnit tests
```

**Adding a language** = one `LanguagePack` (verbs, numbers, default replies, model name) in `Languages/LanguagePack.cs` plus a column in `Localization/Loc.cs`. The tests check that every UI text has a translation.

## Credits & licenses

OK Régis is released under the [MIT License](LICENSE). It uses [Vosk](https://alphacephei.com/vosk/) (Apache 2.0) and its small language models, [NAudio](https://github.com/naudio/NAudio) (MIT), [obs-websocket-dotnet](https://github.com/BarRaider/obs-websocket-dotnet) (MIT) and `System.Speech` for the voice.
