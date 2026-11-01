import { readdir, readFile } from 'node:fs/promises';
import { dirname, extname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const repositoryRoot = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const roots = [resolve(repositoryRoot, 'web/src')];
const allowedExtensions = new Set(['.scss', '.css', '.html', '.ts']);
const forbidden = /(?:linear|radial|conic)-gradient\s*\(/i;
const failures = [];

async function walk(directory) {
  for (const entry of await readdir(directory, { withFileTypes: true })) {
    const path = resolve(directory, entry.name);
    if (entry.isDirectory()) {
      await walk(path);
      continue;
    }

    if (!allowedExtensions.has(extname(entry.name))) {
      continue;
    }

    const content = await readFile(path, 'utf8');
    if (forbidden.test(content)) {
      failures.push(path);
    }
  }
}

for (const root of roots) {
  await walk(root);
}

if (failures.length > 0) {
  console.error(`Gradient styling is forbidden:\n${failures.join('\n')}`);
  process.exit(1);
}

console.log('No gradient styling found in the Angular source.');
