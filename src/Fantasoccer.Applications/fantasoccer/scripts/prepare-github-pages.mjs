import { copyFile, readFile, writeFile } from 'node:fs/promises';
import path from 'node:path';

const outputDirectory = path.resolve(process.argv[2] ?? 'dist');
const configuredBasePath = process.env.EXPO_PUBLIC_BASE_URL?.trim() ?? '';
const basePath =
  configuredBasePath && configuredBasePath !== '/'
    ? `/${configuredBasePath.replace(/^\/+|\/+$/g, '')}`
    : '';
const rootPath = `${basePath}/`;

const indexPath = path.join(outputDirectory, 'index.html');
const index = await readFile(indexPath, 'utf8');
const pagesIndex = index
  .replace('href="/manifest.json"', `href="${basePath}/manifest.json"`)
  .replace(
    ".register('/FCServiceWorker.js', { scope: '/' })",
    `.register('${basePath}/FCServiceWorker.js', { scope: '${rootPath}' })`
  );

await writeFile(indexPath, pagesIndex);
await copyFile(indexPath, path.join(outputDirectory, '404.html'));

const manifestPath = path.join(outputDirectory, 'manifest.json');
const manifest = JSON.parse(await readFile(manifestPath, 'utf8'));
manifest.id = rootPath;
manifest.start_url = rootPath;
manifest.scope = rootPath;
manifest.icons = manifest.icons?.map((icon) => ({
  ...icon,
  src: `${basePath}/${icon.src.replace(/^\/+/, '')}`,
}));

await writeFile(manifestPath, `${JSON.stringify(manifest, null, 2)}\n`);
await writeFile(path.join(outputDirectory, '.nojekyll'), '');
