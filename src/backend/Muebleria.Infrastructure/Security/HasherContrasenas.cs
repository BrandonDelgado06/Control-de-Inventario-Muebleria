using Microsoft.AspNetCore.Identity;
using Muebleria.Application.Common.Interfaces;

namespace Muebleria.Infrastructure.Security;

/// <summary>RNF-01: PBKDF2 con sal aleatoria (PasswordHasher de ASP.NET Core Identity, formato v3).</summary>
public class HasherContrasenas : IHasherContrasenas
{
    private static readonly PasswordHasher<object> Hasher = new();
    private static readonly object Nadie = new();

    public string Hash(string contrasena) => Hasher.HashPassword(Nadie, contrasena);

    public bool Verificar(string hash, string contrasena) =>
        Hasher.VerifyHashedPassword(Nadie, hash, contrasena) != PasswordVerificationResult.Failed;
}
