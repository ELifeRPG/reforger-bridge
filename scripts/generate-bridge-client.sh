#!/usr/bin/env bash
# Regenerates src/ApiClient (the Kiota client for the Central API) from the OpenAPI
# document served by a running Central API (eliferpg-core) instance. Unlike eliferpg-core's
# original version of this script, there's no shared build here to generate the spec from —
# this repo fetches it live over HTTP instead.
#
# Requires a running Central API instance (`dotnet run --project src/Api/Api.csproj` from
# eliferpg-core) reachable at CORE_API_URL (default http://localhost:5100).

set -euo pipefail
cd "$(dirname "$0")/.."

core_api_url="${CORE_API_URL:-http://localhost:5100}"
spec_url="${core_api_url%/}/openapi/v1.json"

echo "Fetching OpenAPI spec from ${spec_url} ..."
dotnet kiota generate \
  --openapi "${spec_url}" \
  --language CSharp \
  --class-name EliferpgApiClient \
  --namespace-name ELifeRPG.Bridge.ApiClient \
  --output src/ApiClient/Generated \
  --clean-output

rm -f src/ApiClient/Generated/.kiota.log

echo "Done. Review the diff in src/ApiClient/Generated/ before committing."
