#!/bin/bash

set -e

echo "Generating OpenAPI specification for DapperMatic API..."

# Get the script directory
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SAMPLE_APP_DIR="$SCRIPT_DIR/sample-app"
DOCS_DIR="$SCRIPT_DIR/.."
API_BROWSER_DIR="$DOCS_DIR/api-browser"

# Create api-browser directory if it doesn't exist
mkdir -p "$API_BROWSER_DIR"

# Build the sample app
echo "Building sample app..."
cd "$SAMPLE_APP_DIR"
dotnet build

# Start the app in the background
echo "Starting sample app to generate OpenAPI spec..."
dotnet run &
APP_PID=$!

# Wait for the app to start
echo "Waiting for app to start..."
sleep 5

# Function to cleanup
cleanup() {
    echo "Stopping sample app..."
    kill $APP_PID 2>/dev/null || true
    wait $APP_PID 2>/dev/null || true
}

# Set trap to cleanup on exit
trap cleanup EXIT

# Fetch the OpenAPI spec
echo "Fetching OpenAPI specification..."
if curl -s -f http://localhost:5000/swagger/v1/swagger.json -o "$API_BROWSER_DIR/openapi.json"; then
    echo "OpenAPI specification written to: $API_BROWSER_DIR/openapi.json"
    echo "OpenAPI specification generation complete!"
else
    echo "Failed to fetch OpenAPI specification"
    exit 1
fi