from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]

def patch(rel, old, new):
    p = ROOT / rel
    text = p.read_text(encoding='utf-8')
    if text.count(old) != 1:
        raise RuntimeError(f'{rel}: expected exactly one match')
    p.write_text(text.replace(old, new, 1), encoding='utf-8', newline='\n')

client_old = 'handler.ClientCertificates.Add(new X509Certificate2(certificatePath, settings.ClientCertificatePassword));'
client_new = '''var contentType = X509Certificate2.GetCertContentType(certificatePath);
            using var certificate = contentType == X509ContentType.Pfx
                ? X509CertificateLoader.LoadPkcs12FromFile(
                    certificatePath,
                    settings.ClientCertificatePassword ?? string.Empty,
                    X509KeyStorageFlags.EphemeralKeySet)
                : X509CertificateLoader.LoadCertificateFromFile(certificatePath);
            if (!certificate.HasPrivateKey)
                throw new InvalidOperationException("Das ALS-Clientzertifikat enthält keinen privaten Schlüssel. Für mTLS ist eine PFX/P12-Datei mit privatem Schlüssel erforderlich.");
            handler.ClientCertificates.Add(certificate);'''
patch('src/Partcounter.App/Services/AlsIntegrationService.cs', client_old, client_new)

pro_old = 'handler.ClientCertificates.Add(new X509Certificate2(path, settings.ClientCertificatePassword));'
pro_new = '''var contentType = X509Certificate2.GetCertContentType(path);
            using var certificate = contentType == X509ContentType.Pfx
                ? X509CertificateLoader.LoadPkcs12FromFile(
                    path,
                    settings.ClientCertificatePassword ?? string.Empty,
                    X509KeyStorageFlags.EphemeralKeySet)
                : X509CertificateLoader.LoadCertificateFromFile(path);
            if (!certificate.HasPrivateKey)
                throw new InvalidOperationException("Das proALPHA-Clientzertifikat enthält keinen privaten Schlüssel. Für mTLS ist eine PFX/P12-Datei mit privatem Schlüssel erforderlich.");
            handler.ClientCertificates.Add(certificate);'''
patch('src/Partcounter.App/Services/ProAlphaIntegrationService.cs', pro_old, pro_new)

upd_old = '''        var certificate = System.Security.Cryptography.X509Certificates.X509Certificate.CreateFromSignedFile(executablePath);
        using var certificate2 = new System.Security.Cryptography.X509Certificates.X509Certificate2(certificate);
        if (string.IsNullOrWhiteSpace(certificate2.Thumbprint))'''
upd_new = '''        using var certificate2 = LoadAuthenticodeSigner(executablePath);
        if (string.IsNullOrWhiteSpace(certificate2.Thumbprint))'''
patch('src/Partcounter.App/Services/PartcounterUpdateService.cs', upd_old, upd_new)

insert_old = '''    private static void VerifyAuthenticode(string executablePath, string expectedThumbprint)
    {'''
insert_new = '''    private static System.Security.Cryptography.X509Certificates.X509Certificate2 LoadAuthenticodeSigner(string executablePath)
    {
        var contentType = System.Security.Cryptography.X509Certificates.X509Certificate2.GetCertContentType(executablePath);
        if (contentType != System.Security.Cryptography.X509Certificates.X509ContentType.Authenticode)
            throw new InvalidDataException("Das Update verlangt Authenticode, aber Partcounter.exe ist nicht Authenticode-signiert.");

        // .NET 10 has no non-obsolete LoadAuthenticodeSigner API. The .NET runtime maintainer
        // recommends this narrowly scoped fallback after an explicit Authenticode content-type check.
#pragma warning disable SYSLIB0057
        return new System.Security.Cryptography.X509Certificates.X509Certificate2(executablePath);
#pragma warning restore SYSLIB0057
    }

    private static void VerifyAuthenticode(string executablePath, string expectedThumbprint)
    {'''
patch('src/Partcounter.App/Services/PartcounterUpdateService.cs', insert_old, insert_new)

print('R001.25 X509 migration applied')
