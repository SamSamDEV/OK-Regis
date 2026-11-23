using OkRegis.Core.Settings;

namespace OkRegis.Core.Localization;

/// <summary>
/// Les textes de l'interface en français, anglais et espagnol.
/// La CLÉ est le texte français : écrire <c>Loc.T("Réglages")</c> donne « Settings » en anglais.
/// Un texte absent de la table s'affiche en français (jamais de plantage).
/// </summary>
public static class Loc
{
    /// <summary>La langue courante (choisie au démarrage ; changer de langue redémarre l'appli).</summary>
    public static Language Current { get; set; } = Language.French;

    public static string T(string french) => Translate(french, Current);

    /// <summary>La traduction d'un texte dans une langue donnée (le texte français lui-même s'il n'est pas dans la table).</summary>
    public static string Translate(string french, Language language)
    {
        if (language == Language.French || !Table.TryGetValue(french, out var t)) return french;
        return language == Language.English ? t.En : t.Es;
    }

    /// <summary>Comme T, avec des valeurs à insérer ({0}, {1}…).</summary>
    public static string T(string french, params object[] args) => string.Format(T(french), args);

    /// <summary>Le nom d'une langue, écrit dans cette langue.</summary>
    public static string NameOf(Language language) => language switch
    {
        Language.English => "English",
        Language.Spanish => "Español",
        _ => "Français",
    };

    /// <summary>Toutes les clés de la table (pour les tests).</summary>
    public static IEnumerable<string> Keys => Table.Keys;

    private static readonly Dictionary<string, (string En, string Es)> Table = new()
    {
        // ---- fenêtre principale ----
        ["Mettre en pause"] = ("Pause listening", "Pausar escucha"),
        ["Reprendre l'écoute"] = ("Resume listening", "Reanudar escucha"),
        ["Mettre l'écoute en pause"] = ("Pause listening", "Pausar escucha"),
        ["Réglages"] = ("Settings", "Ajustes"),
        ["↻  Actualiser"] = ("↻  Refresh", "↻  Actualizar"),
        ["Heure"] = ("Time", "Hora"),
        ["Entendu"] = ("Heard", "Oído"),
        ["Résultat"] = ("Result", "Resultado"),
        ["Scènes"] = ("Scenes", "Escenas"),
        ["Sons"] = ("Sounds", "Sonidos"),
        ["Réponses de Régis"] = ("Régis's replies", "Respuestas de Régis"),
        ["Historique"] = ("History", "Historial"),
        ["En veille (OBS fermé)"] = ("Idle (OBS closed)", "En espera (OBS cerrado)"),
        ["Connexion à OBS…"] = ("Connecting to OBS…", "Conectando con OBS…"),
        ["À l'écoute"] = ("Listening", "Escuchando"),
        ["En pause"] = ("Paused", "En pausa"),
        ["Passer au thème clair"] = ("Switch to the light theme", "Cambiar al tema claro"),
        ["Passer au thème sombre"] = ("Switch to the dark theme", "Cambiar al tema oscuro"),
        ["Dernière phrase entendue : "] = ("Last phrase heard: ", "Última frase oída: "),
        ["Ouvrir la fenêtre"] = ("Open window", "Abrir ventana"),
        ["Quitter"] = ("Quit", "Salir"),
        ["Mo"] = ("MB", "MB"),

        // ---- réponses de Régis ----
        ["Quand je change de scène"] = ("When I switch scene", "Cuando cambio de escena"),
        ["Quand j'affiche une source"] = ("When I show a source", "Cuando muestro una fuente"),
        ["Quand je masque une source"] = ("When I hide a source", "Cuando oculto una fuente"),
        ["Quand je coupe un son"] = ("When I mute a sound", "Cuando silencio un sonido"),
        ["Quand je rétablis un son"] = ("When I unmute a sound", "Cuando reactivo un sonido"),
        ["Quand je monte le volume"] = ("When I raise the volume", "Cuando subo el volumen"),
        ["Quand je baisse le volume"] = ("When I lower the volume", "Cuando bajo el volumen"),
        ["Quand je lance l'enregistrement"] = ("When I start recording", "Cuando inicio la grabación"),
        ["Quand j'arrête l'enregistrement"] = ("When I stop recording", "Cuando detengo la grabación"),
        ["Quand je mets l'enregistrement en pause"] = ("When I pause recording", "Cuando pongo la grabación en pausa"),
        ["Quand je lance le stream"] = ("When I start the stream", "Cuando inicio el stream"),
        ["Quand j'arrête le stream"] = ("When I stop the stream", "Cuando detengo el stream"),
        ["Quand ça n'a pas marché"] = ("When it didn't work", "Cuando no ha funcionado"),
        ["Comment Régis répond ?"] = ("How does Régis reply?", "¿Cómo responde Régis?"),
        ["Voix de Régis (ou mes sons)"] = ("Régis's voice (or my sounds)", "Voz de Régis (o mis sonidos)"),
        ["Juste un bip"] = ("Just a beep", "Solo un pitido"),
        ["Silence"] = ("Silence", "Silencio"),
        ["Écris la phrase que Régis dit ({scène} et {source} sont remplacés par le nom). Si tu choisis un fichier son, il passe avant la voix."]
            = ("Write what Régis says ({scene} and {source} are replaced by the name). A sound file, if you choose one, plays instead of the voice.",
               "Escribe lo que dice Régis ({escena} y {fuente} se sustituyen por el nombre). Si eliges un archivo de sonido, suena en lugar de la voz."),
        ["Phrase que Régis dit…"] = ("What Régis says…", "Lo que dice Régis…"),
        ["▶  Écouter"] = ("▶  Play", "▶  Escuchar"),
        ["Changer le son…"] = ("Change sound…", "Cambiar sonido…"),
        ["Choisir un son…"] = ("Choose a sound…", "Elegir un sonido…"),
        ["Choisir un son"] = ("Choose a sound", "Elegir un sonido"),
        ["Sons (*.wav;*.mp3)|*.wav;*.mp3"] = ("Sounds (*.wav;*.mp3)|*.wav;*.mp3", "Sonidos (*.wav;*.mp3)|*.wav;*.mp3"),
        ["Aucun son : Régis utilise sa voix"] = ("No sound: Régis uses its voice", "Sin sonido: Régis usa su voz"),
        ["Retirer ce son"] = ("Remove this sound", "Quitar este sonido"),

        // ---- scènes, sources, sons ----
        ["● affichée en ce moment   "] = ("● shown right now   ", "● mostrada ahora   "),
        ["{0} sources"] = ("{0} sources", "{0} fuentes"),
        ["{0} source"] = ("{0} source", "{0} fuente"),
        ["Aucune source audio trouvée dans OBS."] = ("No audio source found in OBS.", "No se encontró ninguna fuente de audio en OBS."),
        ["Ouvre OBS : tes sons apparaîtront ici.\nTu pourras couper le micro ou Discord en un clic."]
            = ("Open OBS: your sounds will appear here.\nYou'll be able to mute the mic or Discord with one click.",
               "Abre OBS: tus sonidos aparecerán aquí.\nPodrás silenciar el micro o Discord con un clic."),
        ["Ouvre OBS : tes scènes et leurs sources apparaîtront ici.\nTu pourras leur donner des surnoms."]
            = ("Open OBS: your scenes and their sources will appear here.\nYou'll be able to give them nicknames.",
               "Abre OBS: tus escenas y sus fuentes aparecerán aquí.\nPodrás ponerles apodos."),
        ["Sons (audio)"] = ("Sounds (audio)", "Sonidos (audio)"),
        ["Coupe ou rétablis un son d'un clic. À la voix : « coupe le micro », « mute discord »…"]
            = ("Mute or unmute a sound with one click. By voice: “mute the mic”, “mute discord”…",
               "Silencia o reactiva un sonido con un clic. Por voz: «silencia el micro», «mute discord»…"),
        ["⚠ Régis ne peut pas entendre ce nom : ajoute un surnom"] = ("⚠ Régis can't hear this name: add a nickname", "⚠ Régis no puede oír este nombre: añade un apodo"),
        ["Pas de surnom : dis simplement le nom"] = ("No nickname: just say the name", "Sin apodo: di simplemente el nombre"),
        ["⚠ Régis ne peut pas entendre ce nom → surnom"] = ("⚠ Régis can't hear this name → nickname", "⚠ Régis no puede oír este nombre → apodo"),
        ["⚠ Nom en double : Régis utilisera le premier"] = ("⚠ Duplicate name: Régis will use the first one", "⚠ Nombre duplicado: Régis usará el primero"),
        ["Régis ne connaît pas « {0} ». Essaie un mot plus courant."] = ("Régis doesn't know “{0}”. Try a more common word.", "Régis no conoce «{0}». Prueba una palabra más común."),
        ["Ce mot est déjà le nom d'une autre scène ou source."] = ("This word is already the name of another scene or source.", "Esta palabra ya es el nombre de otra escena o fuente."),
        ["Ce surnom est déjà utilisé pour « {0} »."] = ("This nickname is already used for “{0}”.", "Este apodo ya se usa para «{0}»."),
        ["Écris un surnom…"] = ("Type a nickname…", "Escribe un apodo…"),
        ["Surnom pour « {0} »"] = ("Nickname for “{0}”", "Apodo para «{0}»"),
        ["Écris d'abord un mot."] = ("Type a word first.", "Escribe primero una palabra."),
        ["(nom vide)"] = ("(empty name)", "(nombre vacío)"),
        ["🔇  Coupé"] = ("🔇  Muted", "🔇  Silenciado"),
        ["🔊  Actif"] = ("🔊  On", "🔊  Activo"),
        ["+  Surnom"] = ("+  Nickname", "+  Apodo"),

        // ---- réglages ----
        ["OK Régis — première configuration"] = ("OK Régis — first setup", "OK Régis — primera configuración"),
        ["OK Régis — réglages"] = ("OK Régis — settings", "OK Régis — ajustes"),
        ["Général"] = ("General", "General"),
        ["Commandes"] = ("Commands", "Comandos"),
        ["Coche les commandes que Régis a le droit d'exécuter."] = ("Tick the commands Régis is allowed to run.", "Marca los comandos que Régis puede ejecutar."),
        ["Changer de scène"] = ("Switch scene", "Cambiar de escena"),
        ["Afficher une source"] = ("Show a source", "Mostrar una fuente"),
        ["Masquer une source"] = ("Hide a source", "Ocultar una fuente"),
        ["Couper un son"] = ("Mute a sound", "Silenciar un sonido"),
        ["Rétablir un son"] = ("Unmute a sound", "Reactivar un sonido"),
        ["Monter le volume"] = ("Raise the volume", "Subir el volumen"),
        ["Baisser le volume"] = ("Lower the volume", "Bajar el volumen"),
        ["Lancer l'enregistrement"] = ("Start recording", "Iniciar la grabación"),
        ["Arrêter l'enregistrement"] = ("Stop recording", "Detener la grabación"),
        ["Mettre l'enregistrement en pause"] = ("Pause recording", "Pausar la grabación"),
        ["Lancer le stream (déconseillé)"] = ("Start the stream (not recommended)", "Iniciar el stream (no recomendado)"),
        ["Arrêter le stream"] = ("Stop the stream", "Detener el stream"),
        ["Automatique (comme Windows)"] = ("Automatic (like Windows)", "Automático (como Windows)"),
        ["Clair"] = ("Light", "Claro"),
        ["Sombre (nuit)"] = ("Dark (night)", "Oscuro (noche)"),
        ["Économiser le processeur (n'analyse le micro que quand je parle)"] = ("Save CPU (only analyse the mic while I speak)", "Ahorrar procesador (analiza el micro solo cuando hablo)"),
        ["Lancer OK Régis au démarrage de Windows"] = ("Start OK Régis with Windows", "Iniciar OK Régis con Windows"),
        ["Bienvenue ! Choisis le nom de ton assistant, ton micro, puis active le serveur WebSocket dans OBS :\n"]
            = ("Welcome! Choose your assistant's name and your microphone, then enable the WebSocket server in OBS:\n",
               "¡Bienvenido! Elige el nombre de tu asistente y tu micrófono, y activa el servidor WebSocket en OBS:\n"),
        ["Outils → Paramètres du serveur WebSocket → « Activer le serveur WebSocket » (note le port et le mot de passe)."]
            = ("Tools → WebSocket Server Settings → “Enable WebSocket server” (note the port and the password).",
               "Herramientas → Ajustes del servidor WebSocket → «Habilitar servidor WebSocket» (anota el puerto y la contraseña)."),
        ["Nom de l'assistant"] = ("Assistant name", "Nombre del asistente"),
        ["Langue"] = ("Language", "Idioma"),
        ["Apparence"] = ("Appearance", "Apariencia"),
        ["Micro"] = ("Microphone", "Micrófono"),
        ["Réponses de Régis"] = ("Régis's replies", "Respuestas de Régis"),
        ["Sortie audio des réponses"] = ("Reply audio output", "Salida de audio de las respuestas"),
        ["Adresse d'OBS"] = ("OBS address", "Dirección de OBS"),
        ["Port WebSocket"] = ("WebSocket port", "Puerto WebSocket"),
        ["Mot de passe WebSocket"] = ("WebSocket password", "Contraseña WebSocket"),
        ["Tester la connexion à OBS"] = ("Test the OBS connection", "Probar la conexión con OBS"),
        ["Pas de volume (dB)"] = ("Volume step (dB)", "Paso de volumen (dB)"),
        ["Seuil de confiance"] = ("Confidence threshold", "Umbral de confianza"),
        ["Boost du micro (dB)"] = ("Microphone boost (dB)", "Refuerzo del micrófono (dB)"),
        ["Niveau du micro"] = ("Microphone level", "Nivel del micrófono"),
        ["Micro indisponible pour la mesure."] = ("Microphone unavailable for the measurement.", "Micrófono no disponible para la medición."),
        ["Parle normalement pour tester le niveau."] = ("Speak normally to test the level.", "Habla normalmente para probar el nivel."),
        ["Trop faible : augmente le boost ci-dessus."] = ("Too quiet: raise the boost above.", "Demasiado bajo: sube el refuerzo de arriba."),
        ["Un peu faible : un peu plus de boost aiderait."] = ("A bit quiet: a little more boost would help.", "Un poco bajo: algo más de refuerzo ayudaría."),
        ["Niveau parfait pour la reconnaissance."] = ("Perfect level for recognition.", "Nivel perfecto para el reconocimiento."),
        ["Trop fort : le son sature, baisse le boost."] = ("Too loud: the sound clips, lower the boost.", "Demasiado alto: el sonido satura, baja el refuerzo."),
        ["(micro par défaut)"] = ("(default microphone)", "(micrófono predeterminado)"),
        ["(sortie par défaut)"] = ("(default output)", "(salida predeterminada)"),
        ["Enregistrer"] = ("Save", "Guardar"),
        ["Annuler"] = ("Cancel", "Cancelar"),
        ["⚠ Régis ne pourra pas entendre ce nom : essaie une autre orthographe."] = ("⚠ Régis won't be able to hear this name: try another spelling.", "⚠ Régis no podrá oír este nombre: prueba otra ortografía."),
        ["Connexion…"] = ("Connecting…", "Conectando…"),
        ["✔ Connecté ({0} scènes)"] = ("✔ Connected ({0} scenes)", "✔ Conectado ({0} escenas)"),
        ["Le changement de langue s'applique au redémarrage de l'appli."] = ("The language change applies when the app restarts.", "El cambio de idioma se aplica al reiniciar la aplicación."),
        ["L'appli va redémarrer pour changer de langue."] = ("The app will restart to change the language.", "La aplicación se reiniciará para cambiar el idioma."),

        // ---- démarrage ----
        ["Téléchargement du modèle vocal ({0}, ~40 Mo)…"] = ("Downloading the {0} voice model (~40 MB)…", "Descargando el modelo de voz ({0}, ~40 MB)…"),
        ["Impossible de télécharger le modèle vocal :\n"] = ("Could not download the voice model:\n", "No se pudo descargar el modelo de voz:\n"),
        ["Modèle introuvable après décompression."] = ("Model not found after extraction.", "Modelo no encontrado tras la descompresión."),
        ["Choisis ta langue · Choose your language · Elige tu idioma"] = ("Choose your language · Choisis ta langue · Elige tu idioma", "Elige tu idioma · Choose your language · Choisis ta langue"),
        ["Langue de l'interface et de la reconnaissance vocale"] = ("Language of the interface and of voice recognition", "Idioma de la interfaz y del reconocimiento de voz"),

        // ---- OBS et historique ----
        ["Connexion à OBS impossible (code {0}). Vérifie qu'OBS est ouvert, que le serveur WebSocket est activé, et le port / mot de passe."]
            = ("Cannot connect to OBS (code {0}). Check that OBS is open, that the WebSocket server is enabled, and the port / password.",
               "No se puede conectar con OBS (código {0}). Comprueba que OBS está abierto, que el servidor WebSocket está activado, y el puerto / la contraseña."),
        ["Connexion à OBS refusée : "] = ("OBS refused the connection: ", "OBS rechazó la conexión: "),
        ["OBS ne répond pas"] = ("OBS is not responding", "OBS no responde"),
        ["Source introuvable : {0}"] = ("Source not found: {0}", "Fuente no encontrada: {0}"),
        ["Micro ou modèle vocal indisponible : Régis n'écoute pas"] = ("Microphone or voice model unavailable: Régis is not listening", "Micrófono o modelo de voz no disponible: Régis no escucha"),
        ["Pas assez sûr, ignoré"] = ("Not sure enough, ignored", "No lo bastante seguro, ignorado"),
        ["Pas pour moi"] = ("Not for me", "No es para mí"),
        ["Commande inconnue"] = ("Unknown command", "Comando desconocido"),
        ["Scène ou source inconnue"] = ("Unknown scene or source", "Escena o fuente desconocida"),
        ["Commande désactivée"] = ("Command disabled", "Comando desactivado"),
        ["Échec dans OBS"] = ("Failed in OBS", "Falló en OBS"),
        ["Erreur interne : "] = ("Internal error: ", "Error interno: "),
        ["Le nom est vide."] = ("The name is empty.", "El nombre está vacío."),
        ["Ce nom ressemble au mot de commande « {0} » : Régis risque de se tromper."] = ("This name looks like the command word “{0}”: Régis may get confused.", "Este nombre se parece a la palabra de comando «{0}»: Régis podría confundirse."),
        ["Ce nom est aussi celui d'une scène ou d'une source d'OBS."] = ("This name is also the name of an OBS scene or source.", "Este nombre también es el de una escena o fuente de OBS."),
    };
}
