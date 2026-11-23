# OK Régis

Régisseur vocal pour **OBS** sous Windows. Tu parles à ton assistant comme à une vraie régie
(« Ok Régis, mets LoL », « Régis, affiche AFK », « coupe Spotify »), il exécute l'ordre dans OBS et répond avec une voix.

100 % local (pas d'internet une fois installé), en français, léger : l'appli dort dans la zone de notification
et ne se réveille que quand OBS s'ouvre.

## Installation

1. Installe le **SDK .NET 10** pour compiler (`winget install Microsoft.DotNet.SDK.10`).
2. Compile et publie : `powershell -ExecutionPolicy Bypass -File .\publish.ps1` (Windows bloque souvent les scripts `.ps1` directs) → le dossier `dist` contient `OkRegis.App.exe` (autonome).
3. Lance `OkRegis.App.exe`. **Au premier lancement**, le modèle vocal français léger (`vosk-model-small-fr`, ~40 Mo)
   est téléchargé dans `%AppData%\OK-Regis\models`. (Pour livrer le modèle avec l'appli, dézippe-le dans un dossier
   `models` à côté de l'exe : il sera trouvé en premier.)
4. L'assistant de configuration te demande le nom de ton assistant, ton micro et la connexion à OBS.

## Activer le WebSocket dans OBS

Dans OBS : **Outils → Paramètres du serveur WebSocket** → coche *Activer le serveur WebSocket*.
Note le **port** (4455 par défaut) et le **mot de passe**, puis saisis-les dans OK Régis et clique sur *Tester la connexion à OBS*.
(Il faut OBS 28 ou plus.)

## Exemples de phrases

Forme générale : `[ok|hey] <nom> <commande>`

| Tu dis | Effet |
|---|---|
| « Ok Régis, mets LoL » / « passe sur CS2 » | change de scène |
| « Régis, affiche AFK » / « montre FaceReveal » | affiche une source de la scène affichée |
| « Régis, cache AFK » / « masque chat » / « enlève FaceReveal » | masque la source |
| « Régis, coupe Spotify » / « coupe le micro » | coupe le son |
| « Régis, remets Discord » | rétablit le son |
| « Régis, monte Spotify » / « baisse Discord » | volume ± 3 dB (réglable) |
| « Régis, lance / arrête / pause l'enregistrement » | enregistrement |
| « Régis, lance / arrête le stream » | stream (**désactivé par défaut**, à cocher dans les réglages) |

Chaque type de commande se coche ou se décoche dans les réglages. Si Régis ne comprend pas ou doute,
il ne fait rien et le note dans l'historique.

## Noms que Régis ne peut pas entendre

Vosk ne reconnaît que les mots de son vocabulaire. Les noms comme « Texte (GDI+) 2 » sont signalés avec un ⚠ dans la fenêtre
principale (onglet *Scènes et sources*) : fais un **double-clic** pour leur donner un surnom (« jeu », « alerte »…).
« CS2 » est automatiquement compris comme « c s deux ».

## Vie de l'appli

- Démarre avec Windows (case décochable dans les réglages), cachée dans la zone de notification.
- Quand `obs64.exe` apparaît (vérifié toutes les 3 s), Régis se connecte et ouvre le micro ; quand OBS se ferme, le micro est fermé et la mémoire du modèle libérée.
- Menu de l'icône : ouvrir la fenêtre, mettre l'écoute en pause / reprendre, quitter.
- Processeur et mémoire s'affichent en bas de la fenêtre (rafraîchis seulement quand elle est visible) et au survol de l'icône.
- Pendant que Régis parle, le micro est coupé pour qu'il ne se déclenche pas sur sa propre voix.

## Structure du code

- `src/OkRegis.Core` : toute la logique sans matériel (compréhension des phrases, grammaire, exécution, chef d'orchestre).
- `src/OkRegis.App` : l'appli Windows (OBS WebSocket, micro Vosk/NAudio, voix, icône, fenêtres).
- `tests/OkRegis.Tests` : tests xUnit de la partie Core (`dotnet test`).

## État de vérification

Les tests automatiques passent. Le client OBS, le micro, la voix et l'interface n'ont **pas** encore été vérifiés à la main
avec un vrai OBS et un vrai micro : à contrôler avant de partager (voir la liste dans `docs/superpowers/plans/2026-11-23-ok-regis.md`,
tâches 7 à 12). Les mesures de performance (< 120 Mo, CPU ≈ 0 % en veille) restent à relever.
