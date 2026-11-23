namespace OkRegis.Core.Settings;

/// <summary>Chiffre un secret (mot de passe OBS). L'implémentation Windows (DPAPI) est dans l'appli.</summary>
public interface ISecretProtector
{
    string Protect(string plain);
    string Unprotect(string protectedText);
}
