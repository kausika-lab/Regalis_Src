import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import App from './App';
import { initTheme } from './store/themeStore';
import './styles/globals.css';
import './styles/components.css';
import './styles/layouts.css';
import './styles/auth.css';

// Apply the saved theme before first paint — no flash of the wrong theme.
initTheme();

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <App />
  </StrictMode>,
);
