const fs = require('fs');
const path = require('path');
const dotenv = require('dotenv');

const rootDir = path.resolve(__dirname, '..');
const envPath = path.join(rootDir, '.env');
const outDir = path.join(rootDir, 'src', 'assets');
const outPath = path.join(outDir, 'env.js');

const defaults = {
  API_URL: 'http://localhost:5000/api',
};

let env = { ...defaults };
if (fs.existsSync(envPath)) {
  const parsed = dotenv.parse(fs.readFileSync(envPath));
  env = { ...env, ...parsed };
}

if (!fs.existsSync(outDir)) {
  fs.mkdirSync(outDir, { recursive: true });
}

const content = `window.__env = {
  API_URL: ${JSON.stringify(env.API_URL)}
};
`;

fs.writeFileSync(outPath, content, 'utf8');
console.log(`Generated ${outPath}`);
