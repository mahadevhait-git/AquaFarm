# React TypeScript Frontend for AquaFarm

This is the React with TypeScript frontend for the aquaculture management system.

## Project Structure

```
frontend/
├── public/          # Static files
├── src/
│   ├── components/  # Reusable React components
│   ├── pages/       # Page components (Dashboard, Ponds, Loans, Groups)
│   ├── models/      # TypeScript interfaces and types
│   ├── services/    # API and authentication service wrappers
│   ├── App.tsx      # Main app component with routing
│   ├── App.css      # Global styling
│   └── index.tsx    # React entry point
├── package.json     # Dependencies and scripts
└── tsconfig.json    # TypeScript configuration
```

## Pages

- **Login** (`/login`) - User authentication
- **Dashboard** (`/dashboard`) - Overview of ponds and financial summary
- **Ponds** (`/ponds`) - Pond CRUD operations and transaction history
- **Loans** (`/loans`) - Loan creation, repayment, and interest calculations
- **Groups** (`/groups`) - Farmer group management and shared finances

## API Integration

The frontend communicates with the backend API at `http://localhost:5222/api` (configurable via `.env`).

### Services

- `api.ts` - HTTP wrapper functions for all API endpoints
- `auth.ts` - Token and role management

## Setup

### Prerequisites

- Node.js 18+ and npm

### Installation

```bash
cd frontend
npm install
```

### Development

```bash
npm start
```

The app will start on `http://localhost:3000` and proxy API calls to the backend.

### Build

```bash
npm run build
```

## Environment Variables

Create a `.env` file in the frontend root:

```
REACT_APP_API_URL=http://localhost:5222/api
```

## Demo Credentials

- Username: `farmer1`
- Password: `password123`
- Role: `Farmer`

Or any username/password will work with the demo backend auth service.
