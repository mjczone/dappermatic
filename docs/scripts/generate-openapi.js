#!/usr/bin/env node

// Generates docs/api-browser/openapi.json.
//
// The spec is produced at BUILD time by Microsoft.Extensions.ApiDescription.Server:
// sample-app.csproj sets <OpenApiDocumentsDirectory> to docs/api-browser and
// <OpenApiGenerateDocumentsOptions>--file-name openapi</...>, so a plain `dotnet build`
// writes the document straight into place. No server is started and no port is bound.

import { spawn } from 'child_process';
import { existsSync } from 'fs';
import { mkdir, stat } from 'fs/promises';
import path from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

async function generateOpenApiSpec() {
  console.log('Generating OpenAPI specification for DapperMatic API...');

  const sampleAppPath = path.join(__dirname, 'sample-app');
  const docsPath = path.join(__dirname, '..');
  const apiBrowserPath = path.join(docsPath, 'api-browser');
  const specPath = path.join(apiBrowserPath, 'openapi.json');

  if (!existsSync(apiBrowserPath)) {
    await mkdir(apiBrowserPath, { recursive: true });
  }

  console.log('Building sample app (emits the OpenAPI document as a build artifact)...');
  const build = spawn('dotnet', ['build', '-c', 'Release'], {
    cwd: sampleAppPath,
    stdio: 'inherit',
  });

  const exitCode = await new Promise((resolve) => build.on('close', resolve));
  if (exitCode !== 0) {
    console.error(`Build failed with code ${exitCode}`);
    process.exit(1);
  }

  if (!existsSync(specPath)) {
    console.error(`Build succeeded but no document was written to: ${specPath}`);
    console.error('Check <OpenApiDocumentsDirectory> in sample-app.csproj.');
    process.exit(1);
  }

  const { size } = await stat(specPath);
  console.log(`OpenAPI specification written to: ${specPath} (${size} bytes)`);
  console.log('OpenAPI specification generation complete!');
}

generateOpenApiSpec();
