#!/bin/bash

set -e

echo "Generating OpenAPI specification for DapperMatic API..."

# The spec is produced at BUILD time by Microsoft.Extensions.ApiDescription.Server.
# sample-app.csproj points <OpenApiDocumentsDirectory> at docs/api-browser, so building
# the sample app writes docs/api-browser/openapi.json directly. No server run required.

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SAMPLE_APP_DIR="$SCRIPT_DIR/sample-app"
DOCS_DIR="$SCRIPT_DIR/.."
API_BROWSER_DIR="$DOCS_DIR/api-browser"
SPEC_FILE="$API_BROWSER_DIR/openapi.json"

mkdir -p "$API_BROWSER_DIR"

echo "Building sample app (emits the OpenAPI document as a build artifact)..."
dotnet build "$SAMPLE_APP_DIR" -c Release

if [ -f "$SPEC_FILE" ]; then
    echo "OpenAPI specification written to: $SPEC_FILE"
    echo "OpenAPI specification generation complete!"
else
    echo "Build succeeded but no document was written to: $SPEC_FILE"
    echo "Check <OpenApiDocumentsDirectory> in sample-app.csproj."
    exit 1
fi
