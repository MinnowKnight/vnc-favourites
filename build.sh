#!/bin/sh
# Builds dist/VNCFavourites.exe on Linux/macOS with Mono (apt install mono-devel).
set -e
cd "$(dirname "$0")"
mkdir -p dist
RES=""
for f in src/icons/*.png; do RES="$RES -resource:$f,icon.$(basename "$f" .png).png"; done
mcs -sdk:4.5 -target:winexe -optimize+ -out:dist/VNCFavourites.exe \
  -win32icon:src/app.ico -win32manifest:src/app.manifest \
  -r:System.Windows.Forms.dll -r:System.Drawing.dll -r:System.Security.dll -r:System.Xml.dll \
  $RES src/VNCFavourites.cs
echo "Built dist/VNCFavourites.exe"
