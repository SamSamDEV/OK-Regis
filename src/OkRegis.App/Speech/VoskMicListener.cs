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
    private readonly ModelHost _host;
    private readonly object _lock = new();
    private Model? _model;
    private VoskRecognizer? _recognizer;
    private WaveIn? _waveIn;
    private string? _grammar;
    private bool _muted;
    private readonly Func<bool> _saveCpu;                  // « n'analyser que quand je parle »
    private readonly VoiceGate _gate = new();
    private readonly Queue<byte[]> _preroll = new();       // les dernières tranches de silence, rejouées au début d'une phrase
    private const int PrerollFrames = 3;                   // 300 ms : on ne coupe pas le début des mots

    public event Action<Heard>? PhraseHeard;

    public bool IsRunning { get { lock (_lock) return _waveIn != null; } }

    public VoskMicListener(ModelHost host, Func<bool>? saveCpu = null)
    {
        _host = host;
        _saveCpu = saveCpu ?? (() => false);
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
                if (value) { _recognizer?.Reset(); _gate.Reset(); _preroll.Clear(); } // on oublie ce qui a été capté pendant que Régis parle
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
                _model = _host.Acquire();
                _recognizer = NewRecognizer();
                _waveIn = new WaveIn
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
        _gate.Reset(); _preroll.Clear();
        if (_model != null) { _model = null; _host.Release(); } // le modèle sera libéré s'il n'est plus utilisé
    }

    private void OnData(object? sender, WaveInEventArgs e)
    {
        Heard? heard = null;
        lock (_lock)
        {
            if (_recognizer == null) return;
            if (_muted) { _recognizer.Reset(); return; }

            if (!_saveCpu())
            {
                // Mode classique : tout l'audio va à Vosk.
                if (_recognizer.AcceptWaveform(e.Buffer, e.BytesRecorded)) heard = ParseResult(_recognizer.Result());
            }
            else
            {
                // Mode économe : Vosk ne travaille que pendant la parole (+ une courte traîne).
                var frameMs = Math.Max(1, e.BytesRecorded / 32); // 16 kHz × 2 octets = 32 octets par ms
                var decision = _gate.Next(Rms(e.Buffer, e.BytesRecorded), frameMs);
                if (decision.Start)
                {
                    foreach (var old in _preroll) // le début de la phrase, capté avant la détection
                        if (_recognizer.AcceptWaveform(old, old.Length)) heard = ParseResult(_recognizer.Result()) ?? heard;
                    _preroll.Clear();
                }
                if (decision.Feed)
                {
                    if (_recognizer.AcceptWaveform(e.Buffer, e.BytesRecorded)) heard = ParseResult(_recognizer.Result()) ?? heard;
                }
                else if (decision.End)
                {
                    heard = ParseResult(_recognizer.FinalResult()) ?? heard; // fin de la parole : on demande le résultat
                    _recognizer.Reset();
                }
                else
                {
                    _preroll.Enqueue(e.Buffer.AsSpan(0, e.BytesRecorded).ToArray());
                    while (_preroll.Count > PrerollFrames) _preroll.Dequeue();
                }
            }
        }
        if (heard != null) PhraseHeard?.Invoke(heard); // hors du verrou
    }

    // Volume efficace (0-32768) d'une tranche d'audio 16 bits.
    private static double Rms(byte[] buffer, int count)
    {
        var samples = count / 2;
        if (samples == 0) return 0;
        double sum = 0;
        for (var i = 0; i < samples; i++)
        {
            double s = BitConverter.ToInt16(buffer, i * 2);
            sum += s * s;
        }
        return Math.Sqrt(sum / samples);
    }

    // Vosk répond {"result":[{"conf":0.9,"word":"mets"},...],"text":"mets"}
    private static Heard? ParseResult(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("result", out var result)) return null;
        var words = new List<string>();
        var confs = new List<double>();
        var hasUnknown = false;
        foreach (var w in result.EnumerateArray())
        {
            var word = w.GetProperty("word").GetString() ?? "";
            if (word.Length == 0) continue;
            if (word == "[unk]") { hasUnknown = true; continue; }
            words.Add(word);
            confs.Add(w.GetProperty("conf").GetDouble());
        }
        if (words.Count == 0) return null;
        // Un mot inconnu au milieu ("régis NE coupe PAS spotify") peut changer le sens : confiance 0 = ignoré, mais noté dans l'historique.
        return new Heard(string.Join(' ', words), hasUnknown ? 0.0 : confs.Average());
    }

    private static int FindDevice(string? name)
    {
        if (string.IsNullOrEmpty(name)) return -1; // -1 = micro par défaut de Windows
        for (var i = 0; i < WaveIn.DeviceCount; i++)
            if (name.StartsWith(WaveIn.GetCapabilities(i).ProductName, StringComparison.OrdinalIgnoreCase)) return i;
        return -1;
    }

    /// <summary>Noms des micros disponibles (pour les réglages).</summary>
    public static IReadOnlyList<string> ListDevices() =>
        Enumerable.Range(0, WaveIn.DeviceCount).Select(i => WaveIn.GetCapabilities(i).ProductName).ToList();

    public void Dispose() => Stop();
}
