export const environment = {
  production: false,
  apiUrl: (window as any).__env?.API_URL ?? 'http://localhost:5000/api',
};
