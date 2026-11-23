# OK Régis

[English](README.md) · **Français**

*Vibe coded Voice Command OBS Manager.*

> Un **régisseur vocal pour OBS** sous Windows. Tu lui parles comme à une vraie régie — *« Ok Régis, mets Gameplay »*, *« Régis, cache le chat »*, *« coupe Discord »* — il l'exécute dans OBS et te répond avec une voix.

- 🎙️ **100 % local** : la reconnaissance vocale tourne sur ton PC (Vosk). Pas de cloud, pas de compte, pas d'abonnement.
- 🌍 **Français, anglais et espagnol** : interface *et* commandes vocales.
- 🪶 **Très léger** : ~15 à 40 Mo de mémoire de travail et ≈ 0,3 % d'un cœur de processeur en écoute (détails plus bas).
- 🔌 **Aucun lancement manuel** : démarre avec Windows, dort dans la zone de notification, se réveille à l'ouverture d'OBS et se rendort quand OBS se ferme.
- 🛡️ **Sans risque pour ton live** : c'est une appli à part, **pas un plugin OBS** — si elle plante, OBS continue.

## Téléchargement

Récupère le dernier installateur sur la page [**Releases**](../../releases) : `OKRegis-Setup-x.y.z.exe`.
Il s'installe pour ton utilisateur (sans droits administrateur) et **contient les modèles vocaux des trois langues** — rien d'autre à télécharger.

## Premier lancement

1. Choisis ta langue (Français / English / Español).
2. Dans OBS, active le serveur WebSocket : **Outils → Paramètres du serveur WebSocket → Activer le serveur WebSocket** (OBS 28 ou plus). Note le port (`4455` par défaut) et le mot de passe.
3. Dans OK Régis, choisis le nom de ton assistant (par défaut : *Régis*), ton micro, puis saisis le port et le mot de passe d'OBS. Clique sur **Tester la connexion à OBS**.
4. C'est fini. OK Régis vit dans la zone de notification. Ouvre OBS : il écoute au bout de ~3 secondes.

## Commandes vocales

Forme générale : **`[ok|hey] <nom de l'assistant> <commande>`** — par exemple *« ok Régis mets Gameplay »*.

| | Français | English | Español |
|---|---|---|---|
| Changer de scène | *mets / passe sur / scène* **Gameplay** | *switch to / go to / scene* **Gameplay** | *cambia a / pasa a / escena* **Gameplay** |
| Afficher une source (dans la scène affichée) | *affiche / montre* **webcam** | *show / display / enable* **webcam** | *muestra / enciende / activa* **webcam** |
| Masquer une source | *cache / masque / enlève* **webcam** | *hide / remove / disable* **webcam** | *oculta / esconde / quita / apaga* **webcam** |
| Couper un son | *coupe / mute* **discord** · *coupe le micro* | *mute* **discord** · *mute the mic* | *silencia / mute* **discord** · *silencia el micro* |
| Rétablir | *remets / unmute* **discord** | *unmute* **discord** | *reactiva / unmute* **discord** |
| Volume ± 3 dB | *monte / baisse* **spotify** | *raise / lower* **spotify** | *sube / baja* **spotify** |
| Enregistrement | *lance / arrête / pause l'enregistrement* | *start / stop / pause recording* | *graba · para de grabar · pausa la grabadora* |
| Stream *(désactivé par défaut)* | *lance / arrête le stream* | *start / stop streaming* | *inicia / para el stream* |

Remarques :
- **C'est le verbe qui décide de ce que tu vises.** *« mets »* ne cherche que dans les **scènes** ; *« affiche / cache »* ne cherche que dans les **sources de la scène actuellement affichée** ; *« coupe / monte / baisse »* ne cherchent que dans les **sons**. Une scène et un overlay peuvent porter le même nom sans aucune confusion.
- Les noms sont comparés avec souplesse (casse, accents et ponctuation ignorés, petites fautes tolérées), et un seul mot d'un nom suffit (*« chatting »* pour *« Just Chatting »*).
- Si Régis n'est pas sûr, **il ne fait rien** et le note dans l'historique.
- Chaque type de commande peut être désactivé dans les réglages. **Les commandes de stream sont désactivées par défaut.**
- Pendant que Régis parle, le micro est coupé : il ne se déclenche jamais sur sa propre voix.

## La fenêtre

Ouvre-la depuis l'icône de la zone de notification. En-tête : état, dernière phrase entendue, **Pause**, **Actualiser**, **Réglages** et un **bouton ☀/☾ pour passer du thème clair au thème sombre**.

- **Scènes** — toutes tes scènes comme dans OBS, avec leurs sources dessous (clique sur une scène pour la déplier). Donne un **surnom** à n'importe quel nom : clique sur **+ Surnom**, écris un mot, Entrée. Les noms que le modèle vocal ne peut pas entendre sont signalés par ⚠ — un surnom règle le problème. **Glisse la poignée ⠿ pour réordonner** les scènes ou les sources (l'ordre est mémorisé par l'appli ; OBS n'est pas modifié).
- **Sons** — tes sources audio avec un bouton **Actif / Coupé** en un clic, et des surnoms.
- **Réponses de Régis** — pour chaque action, choisis la phrase que Régis dit (`{scène}` / `{source}` sont remplacés par le nom) et/ou **ton propre fichier son** (wav/mp3, joué à la place de la voix). ▶ permet d'écouter. Ou choisis *juste un bip* ou *silence*.
- **Historique** — tout ce qui a été entendu et ce qui en a été fait.

## Réglages

Langue · nom de l'assistant · apparence · micro · sortie audio des réponses (pour décider si la voix passe en live) · adresse/port/mot de passe d'OBS · pas de volume · seuil de confiance · commandes activées · mode *économiser le processeur* · démarrage avec Windows.

Changer de **langue** redémarre l'appli (un autre modèle vocal est chargé). Les réglages sont dans `%AppData%\OK-Regis\settings.json` ; le mot de passe d'OBS est chiffré avec DPAPI (Windows).

## Performances

Mesuré sur une vraie configuration, OBS ouvert, avec beaucoup de scènes :

| | Valeur |
|---|---|
| Mémoire de travail (Gestionnaire des tâches) | **~15 Mo** au repos, **~40 Mo** après quelques secondes d'écoute |
| Processeur en écoute | **≈ 0,3 %** d'un cœur |
| Processeur quand OBS est fermé | ≈ 0 % (le micro et le modèle vocal sont libérés) |

Comment : le moteur vocal ne travaille que pendant que tu parles (petite détection de voix), la fenêtre n'existe pas en mémoire tant que tu ne l'ouvres pas, la mémoire inutilisée est rendue à Windows après quelques secondes d'inactivité, et la grammaire de reconnaissance ne contient que les phrases valides pour tes scènes actuelles (ce qui la rend aussi plus précise).
Le modèle vocal réserve environ 390 Mo de mémoire *engagée* ; Windows ne garde en RAM que ce dont il a réellement besoin.

## Dépannage

- **Régis ne réagit pas / rarement** — regarde l'onglet **Historique**. *« Pas assez sûr »* → monte le niveau du micro ou baisse le *seuil de confiance*. Rien du tout → décoche **Économiser le processeur** dans les réglages (micros très faibles).
- **⚠ « Régis ne peut pas entendre ce nom »** — le modèle vocal ne connaît pas ce mot (ex. `Texte (GDI+) 2`). Ajoute un surnom avec un mot courant.
- **Connexion à OBS impossible** — OBS doit être ouvert avec le serveur WebSocket activé ; vérifie le port et le mot de passe.
- **Le nom de l'assistant affiche un avertissement** — il est inconnu du modèle vocal ou trop proche d'un mot de commande. Essaie une autre orthographe ou un autre nom.

## Compiler depuis les sources

Nécessite le SDK .NET 10 sous Windows.

```powershell
dotnet test tests/OkRegis.Tests                       # tests unitaires
powershell -ExecutionPolicy Bypass -File publish.ps1  # build autonome dans .\dist
```

Au premier lancement, le modèle vocal de la langue choisie est téléchargé (≈ 40 Mo, depuis alphacephei.com), sauf si un dossier `models` se trouve à côté de l'exécutable. Le script de l'installateur est dans [`installer/`](installer/).

```
src/OkRegis.Core   toute la logique sans matériel : compréhension, grammaire, langues, orchestration (testée)
src/OkRegis.App    l'appli Windows : OBS WebSocket, micro (NAudio + Vosk), voix, icône, fenêtres
tests/             tests xUnit
```

**Ajouter une langue** = un `LanguagePack` (verbes, nombres, réponses par défaut, nom du modèle) dans `Languages/LanguagePack.cs` plus une colonne dans `Localization/Loc.cs`. Les tests vérifient que chaque texte de l'interface a sa traduction.

## Crédits et licences

OK Régis est publié sous [licence MIT](LICENSE). Il utilise [Vosk](https://alphacephei.com/vosk/) (Apache 2.0) et ses modèles de langue légers, [NAudio](https://github.com/naudio/NAudio) (MIT), [obs-websocket-dotnet](https://github.com/BarRaider/obs-websocket-dotnet) (MIT) et `System.Speech` pour la voix.
