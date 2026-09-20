#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"

dotnet publish TrimbleConnector.csproj -c Release -r linux-x64 --self-contained true /p:PublishSingleFile=true -o "./publish/linux-x64"

echo "Published Trimble Connector to publish/linux-x64/TrimbleConnector"
