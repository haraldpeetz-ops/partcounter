from pathlib import Path

patch = Path("tools/apply_r00125_v3_identity_limits.py")
text = patch.read_text(encoding="utf-8")
bad = 'text = replace_once(text, "public void ProtocolV3_BoundaryContractIsAdditive()", "public void ProtocolV3_BoundaryContractIsAdditive()", "protocol test marker")\n'
text = text.replace(bad, "")
code = compile(text, str(patch), "exec")
exec(code, {"__name__": "__main__", "__file__": str(patch)})
