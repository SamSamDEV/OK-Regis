using System.Diagnostics;
using System.Globalization;
using System.Speech.Synthesis;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using OkRegis.Core.Settings;
using OkRegis.Core.Speech;

namespace OkRegis.App.Speech;

/// <summary>
/// Fait répondre Régis : voix Windows, fichier perso (WAV/MP3), bip ou silence.
/// Le son passe par NAudio pour pouvoir choisir la sortie audio (live ou non).
/// </summary>
public class WindowsResponder : IResponder
{
    private readonly AppSettings _settings;

    public event Action? SpeakingStarted;
    public event Action? SpeakingEnded;

    public WindowsResponder(AppSettings settings) => _settings = settings;

    public async Task RespondAsync(string actionKey, string text)
    {
        if (_settings.VoiceMode == ResponseMode.Silent) return;
        SpeakingStarted?.Invoke(); // Régis va parler : le micro se coupe
        try
        {
            switch (_settings.VoiceMode)
            {
                case ResponseMode.Beep:
                    await PlayAsync(Beep());
                    break;
                case ResponseMode.WindowsVoice or ResponseMode.CustomFiles
                    when _settings.CustomResponseFiles.TryGetValue(actionKey, out var file) && File.Exists(file): // un son choisi passe avant la voix
                    using (var reader = new AudioFileReader(file)) await PlayAsync(reader);
                    break;
                default: // voix Windows (aussi quand le fichier perso est absent)
                    if (text.Length > 0) await SpeakAsync(text);
                    break;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine("Réponse impossible : " + ex.Message);
        }
        finally
        {
            await Task.Delay(300); // laisse mourir l'écho avant de réécouter
            SpeakingEnded?.Invoke();
        }
    }

    // La synthèse est créée à la demande puis jetée : pas de mémoire occupée entre deux réponses.
    private async Task SpeakAsync(string text)
    {
        using var ms = new MemoryStream();
        using (var synth = new SpeechSynthesizer())
        {
            try { synth.SelectVoiceByHints(VoiceGender.NotSet, VoiceAge.NotSet, 0, new CultureInfo("fr-FR")); }
            catch (Exception) { /* pas de voix française : voix par défaut */ }
            synth.SetOutputToWaveStream(ms);
            synth.Speak(text);
        }
        ms.Position = 0;
        using var reader = new WaveFileReader(ms);
        await PlayAsync(reader);
    }

    private static IWaveProvider Beep() =>
        new SignalGenerator(44100, 1) { Frequency = 880, Type = SignalGeneratorType.Sin, Gain = 0.2 }
            .Take(TimeSpan.FromMilliseconds(150)).ToWaveProvider();

    private async Task PlayAsync(IWaveProvider source)
    {
        var done = new TaskCompletionSource();
        using var output = new WaveOutEvent { DeviceNumber = FindOutput(_settings.OutputDeviceName) };
        output.PlaybackStopped += (_, _) => done.TrySetResult();
        output.Init(source);
        output.Play();
        await done.Task;
    }

    private static int FindOutput(string? name)
    {
        if (string.IsNullOrEmpty(name)) return -1; // sortie par défaut de Windows
        for (var i = 0; i < WaveOut.DeviceCount; i++)
            if (name.StartsWith(WaveOut.GetCapabilities(i).ProductName, StringComparison.OrdinalIgnoreCase)) return i;
        return -1;
    }

    /// <summary>Noms des sorties audio disponibles (pour les réglages).</summary>
    public static IReadOnlyList<string> ListDevices() =>
        Enumerable.Range(0, WaveOut.DeviceCount).Select(i => WaveOut.GetCapabilities(i).ProductName).ToList();
}
