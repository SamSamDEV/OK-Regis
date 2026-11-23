using System.Diagnostics;
using System.Text.Json;
using NAudio.Wave;
using OkRegis.Core.Speech;
using Vosk;

namespace OkRegis.App.Speech;

/// <summary>
/// Écoute le micro (16 kHz mono) et reconnaît les phrases avec Vosk, restreint à la grammaire donnée.
/// Le modèle (~40 Mo) n'est chargé que pendant l'écoute : en veille, rien n'occupe la mémoire ni le micro.
/// </summary>
public class VoskMicListener : IMicListener
{
    private readonly string _modelPath;
    private readonly object _lock = new();
    private Model? _model;
    private VoskRecognizer? _recognizer;
    private WaveInEvent? _waveIn;
    private string? _grammar;
    private bool _muted;

    public event Action<Heard>? PhraseHeard;

    public VoskMicListener(string modelPath)
    {
        _modelPath = modelPath;
        Vosk.Vosk.SetLogLevel(-1); // pas de messages de debug de Vosk
    }

    public bool Muted
    {
        get => _muted;
        set
        {
            lock (_lock)
            {
                _muted = value;
                if (value) _recognizer?.Reset(); // on oublie ce qui a été capté pendant que Régis parle
            }
        }
    }

    public void Start(string? micDeviceName)
    {
        lock (_lock)
        {
            if (_waveIn != null) return;
            try
            {
                _model ??= new Model(_modelPath);
                _recognizer = NewRecognizer();
                _waveIn = new WaveInEvent
                {
                    DeviceNumber = FindDevice(micDeviceName),
                    WaveFormat = new WaveFormat(16000, 16, 1),
                    BufferMilliseconds = 100,
                };
                _waveIn.DataAvailable += OnData;
                _waveIn.StartRecording();
            }
            catch (Exception ex)
            {
                // Micro débranché, modèle absent... on reste à l'arrêt, sans planter.
                Debug.WriteLine("Micro impossible à ouvrir : " + ex.Message);
                StopInternal();
            }
        }
    }

    public void Stop()
    {
        lock (_lock) StopInternal();
    }

    public void SetGrammar(string voskJsonGrammar)
    {
        lock (_lock)
        {
            _grammar = voskJsonGrammar;
            if (_waveIn == null || _model == null) return;
            var old = _recognizer;
            _recognizer = NewRecognizer(); // on remplace le recognizer sans fermer le micro
            old?.Dispose();
        }
    }

    private VoskRecognizer NewRecognizer()
    {
        var r = _grammar is null ? new VoskRecognizer(_model, 16000f) : new VoskRecognizer(_model, 16000f, _grammar);
        r.SetWords(true); // nécessaire pour avoir la confiance mot par mot
        return r;
    }

    private void StopInternal()
    {
        if (_waveIn != null)
        {
            _waveIn.DataAvailable -= OnData;
            try { _waveIn.StopRecording(); } catch (Exception) { }
            _waveIn.Dispose();
            _waveIn = null;
        }
        _recognizer?.Dispose(); _recognizer = null;
        _model?.Dispose(); _model = null; // libère les ~40 Mo en veille
    }

    private void OnData(object? sender, WaveInEventArgs e)
    {
        Heard? heard = null;
        lock (_lock)
        {
            if (_recognizer == null) return;
            if (_muted) { _recognizer.Reset(); return; }
            if (_recognizer.AcceptWaveform(e.Buffer, e.BytesRecorded))
                heard = ParseResult(_recognizer.Result());
        }
        if (heard != null) PhraseHeard?.Invoke(heard); // hors du verrou
    }

    // Vosk répond {"result":[{"conf":0.9,"word":"mets"},...],"text":"mets"}
    private static Heard? ParseResult(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("result", out var result)) return null;
        var words = new List<string>();
        var confs = new List<double>();
        foreach (var w in result.EnumerateArray())
        {
            var word = w.GetProperty("word").GetString() ?? "";
            if (word == "[unk]" || word.Length == 0) continue;
            words.Add(word);
            confs.Add(w.GetProperty("conf").GetDouble());
        }
        return words.Count == 0 ? null : new Heard(string.Join(' ', words), confs.Average());
    }

    private static int FindDevice(string? name)
    {
        if (string.IsNullOrEmpty(name)) return 0;
        for (var i = 0; i < WaveInEvent.DeviceCount; i++)
            if (name.StartsWith(WaveInEvent.GetCapabilities(i).ProductName, StringComparison.OrdinalIgnoreCase)) return i;
        return 0;
    }

    /// <summary>Noms des micros disponibles (pour les réglages).</summary>
    public static IReadOnlyList<string> ListDevices() =>
        Enumerable.Range(0, WaveInEvent.DeviceCount).Select(i => WaveInEvent.GetCapabilities(i).ProductName).ToList();

    public void Dispose() => Stop();
}
