using Vosk;

namespace OkRegis.App.Speech;

/// <summary>
/// Garde UN SEUL modèle Vosk chargé, partagé entre l'écoute et la vérification des mots.
/// Le modèle est libéré 20 s après son dernier usage quand personne n'écoute : en veille, il n'occupe plus la mémoire.
/// </summary>
public class ModelHost : IDisposable
{
    private readonly string _path;
    private readonly object _lock = new();
    private readonly System.Threading.Timer _idleTimer;
    private Model? _model;
    private int _holders;

    public ModelHost(string path)
    {
        _path = path;
        _idleTimer = new System.Threading.Timer(_ => ReleaseIfIdle(), null, Timeout.Infinite, Timeout.Infinite);
    }

    /// <summary>Utilisé par l'écoute : le modèle reste chargé jusqu'à Release.</summary>
    public Model Acquire()
    {
        lock (_lock)
        {
            _holders++;
            _idleTimer.Change(Timeout.Infinite, Timeout.Infinite);
            return _model ??= new Model(_path);
        }
    }

    public void Release()
    {
        lock (_lock)
        {
            if (_holders > 0) _holders--;
            if (_holders == 0) _idleTimer.Change(20_000, Timeout.Infinite);
        }
    }

    /// <summary>Le modèle connaît-il ce mot (écrit exactement) ?</summary>
    public bool WordExists(string word)
    {
        lock (_lock)
        {
            _model ??= new Model(_path);
            if (_holders == 0) _idleTimer.Change(20_000, Timeout.Infinite); // un seul déclenchement, pas de boucle
            return _model.FindWord(word) != -1;
        }
    }

    private void ReleaseIfIdle()
    {
        lock (_lock)
        {
            if (_holders > 0) return;
            _model?.Dispose();
            _model = null;
        }
    }

    public void Dispose()
    {
        _idleTimer.Dispose();
        lock (_lock) { _model?.Dispose(); _model = null; }
    }
}
