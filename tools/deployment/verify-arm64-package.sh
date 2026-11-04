#!/usr/bin/env bash
set -euo pipefail
[ "$#" -eq 1 ] || { echo 'Usage: verify-arm64-package.sh ZIP' >&2; exit 2; }
[ "$(uname -s)" = Linux ] && [ "$(uname -m)" = aarch64 ] || { echo 'Linux ARM64 runner required' >&2; exit 1; }
[ "$(dotnet --version)" = 10.0.403 ] || { echo 'SDK 10.0.403 required' >&2; exit 1; }
# Only extract an archive that has passed validate-package.sh. Recheck paths here
# because this script can also be invoked independently.
tmp=$(mktemp -d)
trap 'rm -rf "$tmp"' EXIT
python3 - "$1" "$tmp/package" <<'PY'
import pathlib, sys, zipfile
with zipfile.ZipFile(sys.argv[1]) as z:
    names = z.namelist()
    if len(names) != len(set(names)): raise SystemExit('duplicate ZIP entry')
    for i in z.infolist():
        n = i.filename
        if n.startswith(('/', '\\')) or '\\' in n or any(p in ('', '.', '..') for p in n.rstrip('/').split('/')) or (i.external_attr >> 16) & 0o170000 == 0o120000:
            raise SystemExit('unsafe ZIP entry')
    if z.testzip() is not None: raise SystemExit('corrupt ZIP')
    z.extractall(sys.argv[2])
PY
package="$tmp/package"
[ -f "$package/SkiaSharp.dll" ] || { echo 'SkiaSharp assembly missing' >&2; exit 1; }
native=$(find "$package" -name libSkiaSharp.so -type f -print -quit)
[ -n "$native" ] || { echo 'SkiaSharp native library missing' >&2; exit 1; }
mkdir "$tmp/probe"
cat > "$tmp/probe/Probe.csproj" <<EOF2
<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework></PropertyGroup><ItemGroup><Reference Include="SkiaSharp"><HintPath>$package/SkiaSharp.dll</HintPath></Reference></ItemGroup></Project>
EOF2
cat > "$tmp/probe/Program.cs" <<'EOF2'
using SkiaSharp;
using var bitmap = new SKBitmap(2, 2);
using var canvas = new SKCanvas(bitmap);
canvas.Clear(SKColors.Red);
using var image = SKImage.FromBitmap(bitmap);
using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
if (encoded == null || encoded.Size == 0) throw new System.Exception("Native media probe failed");
EOF2
export LD_LIBRARY_PATH="$(dirname "$native")${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}"
dotnet run --project "$tmp/probe/Probe.csproj" --configuration Release --verbosity quiet
# Boot the exact packaged executable with synthetic configuration and probe liveness.
# Readiness requires a separately provisioned disposable database.
python3 - "$package" <<'PY2'
import os, socket, subprocess, sys, time, urllib.request
with socket.socket() as sock:
    sock.bind(('127.0.0.1', 0)); port = sock.getsockname()[1]
env = dict(os.environ, ASPNETCORE_URLS=f'http://127.0.0.1:{port}',
    ConnectionStrings__PostgreSql='Host=127.0.0.1;Database=mc_test_probe;Username=synthetic;Password=synthetic',
    Authentication__SigningKey='synthetic-signing-key-for-package-probe-only',
    Authentication__Issuer='synthetic', Authentication__Audience='synthetic')
env.pop('AWS_LAMBDA_FUNCTION_NAME', None)
with subprocess.Popen(['dotnet', 'MuscleCuties.Server.dll'], cwd=sys.argv[1], env=env, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL) as process:
    try:
        for attempt in range(60):
            if process.poll() is not None: raise SystemExit('packaged server failed to start')
            try:
                with urllib.request.urlopen(f'http://127.0.0.1:{port}/health/live', timeout=1) as response:
                    if response.status == 200: break
            except OSError: pass
            time.sleep(.5)
        else: raise SystemExit('packaged server liveness timed out')
    finally:
        process.terminate()
        try: process.wait(timeout=5)
        except subprocess.TimeoutExpired: process.kill(); process.wait()
print('ARM64 native media and packaged server liveness passed')
PY2
