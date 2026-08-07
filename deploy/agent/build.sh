#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
DIST="$ROOT/deploy/agent/dist"

echo "==> Publicando agente Legacy (Windows 7, x86, self-contained) <=="
dotnet publish "$ROOT/src/Agent/EcDataguard.Agent.Legacy/EcDataguard.Agent.Legacy.csproj" \
  -c Release -r win-x86 --self-contained true -p:PublishSingleFile=true \
  -o "$DIST/win7"

echo "==> Publicando agente moderno (Windows 10/11, x64) <=="
dotnet publish "$ROOT/src/Agent/EcDataguard.Agent/EcDataguard.Agent.csproj" \
  -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true \
  -o "$DIST/win10"

echo "==> Publicando agente Linux (x64) <=="
dotnet publish "$ROOT/src/Agent/EcDataguard.Agent/EcDataguard.Agent.csproj" \
  -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true \
  -o "$DIST/linux"
mv -f "$DIST/linux/EcDataguardAgent" "$DIST/linux/ecdataguard-agent"

sha256() { sha256sum "$1" | awk '{print "sha256:" $1}'; }

cat > "$DIST/manifest.json" <<EOF
{
  "product": "EcDataguard Agent",
  "version": "1.0.0",
  "builds": [
    { "os": "windows", "tag": "win7",  "arch": "x86",  "rid": "win-x86",     "framework": "net6.0-windows7.0", "file": "EcDataguardAgent7.exe", "path": "win7/EcDataguardAgent7.exe",  "sha256": "$(sha256 $DIST/win7/EcDataguardAgent7.exe)",  "size": $(stat -c%s $DIST/win7/EcDataguardAgent7.exe) },
    { "os": "windows", "tag": "win10", "arch": "x64",  "rid": "win-x64",     "framework": "net8.0",             "file": "EcDataguardAgent.exe",   "path": "win10/EcDataguardAgent.exe", "sha256": "$(sha256 $DIST/win10/EcDataguardAgent.exe)",  "size": $(stat -c%s $DIST/win10/EcDataguardAgent.exe) },
    { "os": "linux",   "tag": "linux", "arch": "x64", "rid": "linux-x64",    "framework": "net8.0",             "file": "ecdataguard-agent",      "path": "linux/ecdataguard-agent",     "sha256": "$(sha256 $DIST/linux/ecdataguard-agent)",       "size": $(stat -c%s $DIST/linux/ecdataguard-agent) }
  ]
}
EOF

echo "==> Manifiesto: $DIST/manifest.json"
echo "Listo. Sirva el contenido de '$DIST' como descarga pública (volumen 'agents')."