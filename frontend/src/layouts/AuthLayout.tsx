import { Outlet } from 'react-router-dom';

/** Centered panel on the dark brand background — login, register, OTP, reset. */
export function AuthLayout() {
  return (
    <div className="auth-layout">
      <div className="auth-layout__panel">
        <div className="auth-layout__brand">
          <img className="auth-layout__logo" src="/icons/icon.svg" alt="" />
          <div className="auth-layout__name">
            REGALIS <em>WORKSPACE</em>
          </div>
          <p className="auth-layout__tagline">Premium coworking, on your schedule.</p>
        </div>
        <Outlet />
      </div>
    </div>
  );
}
