#!/usr/bin/env node
/**
 * Apply test method renames from CSV file.
 * Simple approach: read each CSV line, open file, replace, close.
 */

const fs = require('fs');
const path = require('path');

const BASE_DIR = __dirname;
const CSV_FILE = path.join(BASE_DIR, 'test_renames.csv');

function main() {
  console.log('Reading CSV file:', CSV_FILE);

  // Read CSV file
  const csvContent = fs.readFileSync(CSV_FILE, 'utf8');
  const lines = csvContent.split('\n').filter(line => line.trim());

  if (lines.length === 0) {
    console.error('CSV file is empty');
    process.exit(1);
  }

  // Skip header
  const header = lines[0];
  console.log('CSV Header:', header);
  console.log(`Processing ${lines.length - 1} renames...\n`);

  let totalRenames = 0;
  const errors = [];

  // Process each CSV line
  for (let i = 1; i < lines.length; i++) {
    const line = lines[i];
    const parts = line.split(',');

    if (parts.length < 3) {
      console.warn(`Skipping invalid line ${i + 1}: ${line}`);
      continue;
    }

    const relFilePath = parts[0].trim();
    const oldName = parts[1].trim();
    const newName = parts[2].trim();

    // Skip if no change needed
    if (oldName === newName) {
      continue;
    }

    const filePath = path.join(BASE_DIR, relFilePath);

    // Check if file exists
    if (!fs.existsSync(filePath)) {
      errors.push(`File not found: ${filePath}`);
      continue;
    }

    // Read file content
    let content = fs.readFileSync(filePath, 'utf8');

    // Check if old name exists in file
    if (!content.includes(oldName)) {
      errors.push(`⚠️  Method '${oldName}' not found in ${relFilePath}`);
      continue;
    }

    // Count occurrences
    const occurrences = (content.match(new RegExp(oldName.replace(/[.*+?^${}()|[\]\\]/g, '\\$&'), 'g')) || []).length;

    if (occurrences > 1) {
      errors.push(`⚠️  Method '${oldName}' appears ${occurrences} times in ${relFilePath} - SKIPPING for safety`);
      continue;
    }

    // Replace old name with new name
    content = content.replace(oldName, newName);

    // Write back
    try {
      fs.writeFileSync(filePath, content, 'utf8');
      console.log(`✓ ${relFilePath}: ${oldName} → ${newName}`);
      totalRenames++;
    } catch (err) {
      errors.push(`Error writing ${filePath}: ${err.message}`);
    }
  }

  // Summary
  console.log(`\n${'='.repeat(80)}`);
  console.log('SUMMARY');
  console.log('='.repeat(80));
  console.log(`Total renames applied: ${totalRenames}`);

  if (errors.length > 0) {
    console.log(`\n${errors.length} errors/warnings:`);
    errors.forEach(error => console.log(`  ${error}`));
    process.exit(1);
  } else {
    console.log('✓ All renames completed successfully!');
  }
}

if (require.main === module) {
  main();
}
