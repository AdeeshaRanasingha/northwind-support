export function safeReturnPath(state: unknown): string {
  if (typeof state !== 'object' || state === null || !('returnTo' in state)
    || typeof state.returnTo !== 'string') return '/tickets';
  const path = state.returnTo;
  if (!path.startsWith('/') || path.startsWith('//') || /[\\\s%]/.test(path.split(/[?#]/)[0])
    || [...path].some((character) => character.charCodeAt(0) < 32 || character.charCodeAt(0) === 127)) {
    return '/tickets';
  }
  const url = new URL(path, 'http://supportdesk.local');
  if (url.origin !== 'http://supportdesk.local' || /^\/login\/?$/i.test(url.pathname)) return '/tickets';
  return `${url.pathname}${url.search}${url.hash}`;
}
