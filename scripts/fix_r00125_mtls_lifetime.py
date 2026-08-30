from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]

for rel in [
    'src/Partcounter.App/Services/AlsIntegrationService.cs',
    'src/Partcounter.App/Services/ProAlphaIntegrationService.cs',
]:
    path = ROOT / rel
    text = path.read_text(encoding='utf-8')
    old = 'using var certificate = contentType == X509ContentType.Pfx'
    if text.count(old) != 1:
        raise RuntimeError(f'{rel}: expected one certificate lifetime marker')
    text = text.replace(old, 'var certificate = contentType == X509ContentType.Pfx', 1)
    path.write_text(text, encoding='utf-8', newline='\n')

print('R001.25 mTLS certificate lifetime fixed')
