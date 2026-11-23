using System.Security.Cryptography;
using System.Text;
using OkRegis.Core.Settings;

namespace OkRegis.App;

/// <summary>Chiffre le mot de passe avec DPAPI : seul cet utilisateur Windows peut le relire.</summary>
public class DpapiSecretProtector : ISecretProtector
{
    public string Protect(string plain) => Convert.ToBase64String(
        ProtectedData.Protect(Encoding.UTF8.GetBytes(plain), null, DataProtectionScope.CurrentUser));

    public string Unprotect(string protectedText) => Encoding.UTF8.GetString(
        ProtectedData.Unprotect(Convert.FromBase64String(protectedText), null, DataProtectionScope.CurrentUser));
}
