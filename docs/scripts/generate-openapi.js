#!/usr/bin/env node

import { spawn } from 'child_process';
import { promisify } from 'util';
import { writeFile, mkdir } from 'fs/promises';
import { existsSync } from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

const execAsync = promisify(spawn);

async function generateOpenApiSpec() {
  console.log('Generating OpenAPI specification for DapperMatic API...');

  const sampleAppPath = path.join(__dirname, 'sample-app');
  const docsPath = path.join(__dirname, '..');
  const apiBrowserPath = path.join(docsPath, 'api-browser');

  try {
    // Ensure api-browser directory exists
    if (!existsSync(apiBrowserPath)) {
      await mkdir(apiBrowserPath, { recursive: true });
    }

    // Build the sample app
    console.log('Building sample app...');
    const buildProcess = spawn('dotnet', ['build'], {
      cwd: sampleAppPath,
      stdio: 'inherit'
    });

    await new Promise((resolve, reject) => {
      buildProcess.on('close', (code) => {
        if (code === 0) {
          resolve();
        } else {
          reject(new Error(`Build failed with code ${code}`));
        }
      });
    });

    console.log('Starting sample app to generate OpenAPI spec...');

    // Start the app in the background
    const appProcess = spawn('dotnet', ['run'], {
      cwd: sampleAppPath,
      env: { ...process.env, ASPNETCORE_ENVIRONMENT: 'Development' },
      stdio: 'pipe'
    });

    // Monitor app output to detect when it's ready
    let appReady = false;
    appProcess.stdout.on('data', (data) => {
      const output = data.toString();
      console.log(output);
      if (output.includes('Now listening on:') || output.includes('Application started')) {
        appReady = true;
      }
    });

    appProcess.stderr.on('data', (data) => {
      console.error(data.toString());
    });

    // Wait for the app to start (increased timeout)
    await new Promise(resolve => setTimeout(resolve, 8000));

    // Fetch the OpenAPI spec
    console.log('Fetching OpenAPI specification...');
    const response = await fetch('http://localhost:5000/swagger/v1/swagger.json');

    if (!response.ok) {
      throw new Error(`Failed to fetch OpenAPI spec: ${response.status} ${response.statusText}`);
    }

    const openApiSpec = await response.json();

    // Kill the app process (Windows-compatible)
    if (process.platform === 'win32') {
      const killProcess = spawn('taskkill', ['/pid', appProcess.pid, '/f', '/t'], { stdio: 'inherit' });
      await new Promise(resolve => killProcess.on('close', resolve));
    } else {
      appProcess.kill('SIGTERM');
      await new Promise(resolve => appProcess.on('close', resolve));
    }

    // Small delay to ensure cleanup
    await new Promise(resolve => setTimeout(resolve, 500));

    // Write the OpenAPI spec to file
    const specPath = path.join(apiBrowserPath, 'openapi.json');
    await writeFile(specPath, JSON.stringify(openApiSpec, null, 2));
    console.log(`OpenAPI specification written to: ${specPath}`);

    console.log('OpenAPI specification generation complete!');

  } catch (error) {
    console.error('Error generating OpenAPI specification:', error);
    process.exit(1);
  }
}

generateOpenApiSpec();