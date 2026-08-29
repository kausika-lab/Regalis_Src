import { Outlet } from 'react-router-dom';

/**
 * Auth shell per the "Regalis Login" design: parchment ground with dotted-grid
 * and ring ornaments; each screen renders its own panel content. The crown +
 * wordmark brand row is shared.
 */
export function AuthLayout() {
  return (
    <div className="auth">
      <div className="auth__dots auth__dots--tl" aria-hidden />
      <div className="auth__dots auth__dots--br" aria-hidden />
      <div className="auth__ring auth__ring--tr" aria-hidden />
      <div className="auth__ring auth__ring--bl" aria-hidden />
      <Outlet />
    </div>
  );
}

export function AuthBrand() {
  return (
    <div className="auth__brand">
      <img className="auth__crown" src="/brand/regalis-crown.png" alt="" />
      <span className="auth__wordmark">REGALIS</span>
    </div>
  );
}
