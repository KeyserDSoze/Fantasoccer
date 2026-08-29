const configuredBasePath = process.env.EXPO_PUBLIC_BASE_URL?.trim() ?? '';

export const webBasePath =
  configuredBasePath && configuredBasePath !== '/'
    ? `/${configuredBasePath.replace(/^\/+|\/+$/g, '')}`
    : '';

export const toWebPath = (path = '/'): string => {
  const normalizedPath = path.startsWith('/') ? path : `/${path}`;
  return `${webBasePath}${normalizedPath}`;
};

export const getWebUrl = (path = '/'): string => {
  const origin = typeof window === 'undefined' ? 'http://localhost:3000' : window.location.origin;
  return `${origin}${toWebPath(path)}`;
};

export const stripWebBasePath = (path: string): string => {
  if (!webBasePath) return path;
  if (path === webBasePath) return '/';
  return path.startsWith(`${webBasePath}/`) ? path.slice(webBasePath.length) : path;
};
