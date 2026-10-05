#!/usr/bin/env bash
set -euo pipefail

: "${SUPABASE_URL:?SUPABASE_URL is required}"
: "${SUPABASE_ANON_KEY:?SUPABASE_ANON_KEY is required}"

# Render's static build environment may not have the required .NET SDK.
# Install .NET 9 locally in the build environment when needed.
if ! command -v dotnet >/dev/null 2>&1; then
  curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
  bash /tmp/dotnet-install.sh --channel 9.0 --install-dir "$HOME/.dotnet" --no-path
  export DOTNET_ROOT="$HOME/.dotnet"
  export PATH="$DOTNET_ROOT:$PATH"
fi

cat > wwwroot/config.json <<EOF
{
  "supabaseUrl": "${SUPABASE_URL%/}",
  "supabaseAnonKey": "$SUPABASE_ANON_KEY"
}
EOF

dotnet --info
dotnet restore
dotnet publish SmartNestVisionDashboard.csproj -c Release -o publish
