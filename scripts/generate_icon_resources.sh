#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
focuson_assets=src/FocusOn/Assets
if command -v magick >/dev/null; then focuson_convert=magick; else focuson_convert=convert; fi
"$focuson_convert" "$focuson_assets/focuson-master.png" -resize 256x256 "$focuson_assets/focuson.png"
"$focuson_convert" "$focuson_assets/focuson-master.png" -resize 48x48 "$focuson_assets/focuson-tray.png"
"$focuson_convert" "$focuson_assets/focuson-template-master.png" -resize 44x44 -gravity center -background none -extent 44x44 "$focuson_assets/focuson-template.png"
"$focuson_convert" "$focuson_assets/focuson-master.png" -define icon:auto-resize=256,128,64,48,32,24,16 "$focuson_assets/focuson.ico"
for focuson_size in 16 24 32 48 64 128 256 512 1024; do
  focuson_directory="$focuson_assets/hicolor/${focuson_size}x${focuson_size}/apps"
  mkdir -p "$focuson_directory"
  "$focuson_convert" "$focuson_assets/focuson-master.png" -resize "${focuson_size}x${focuson_size}" "$focuson_directory/focuson.png"
done
python3 - <<'PY'
# Container format only: embed the generated PNG sizes in a multiresolution ICNS.
from pathlib import Path
from struct import pack
root = Path('src/FocusOn/Assets')
chunks = []
for kind, size in [('icp4',16),('icp5',32),('icp6',64),('ic07',128),('ic08',256),('ic09',512),('ic10',1024)]:
    png = (root / f'hicolor/{size}x{size}/apps/focuson.png').read_bytes()
    chunks.append(kind.encode() + pack('>I',len(png)+8) + png)
body = b''.join(chunks)
(root / 'focuson.icns').write_bytes(b'icns' + pack('>I',len(body)+8) + body)
PY
