import axios, { AxiosError, type AxiosRequestConfig } from 'axios';
import type { ApiResponse } from '@/models/api';
import { useAuthStore } from '@/store/authStore';

/**
 * The single axios instance (ARCHITECTURE §10). Interceptors attach the bearer
 * token, refresh once on 401 (single-flight, queued retries) and log out on
 * repeated failure. ApiResponse.errors is surfaced to callers via ApiClientError.
 */
export const api = axios.create({
  baseURL: '/api',
  timeout: 30_000,
  headers: { 'Content-Type': 'application/json' },
});

export class ApiClientError extends Error {
  constructor(
    public readonly response: ApiResponse | null,
    public readonly status: number | null,
  ) {
    super(response?.message ?? 'Unable to reach the server. Please check your connection.');
    this.name = 'ApiClientError';
  }

  get errors() {
    return this.response?.errors ?? [];
  }

  hasCode(code: string) {
    return this.errors.some((e) => e.code === code);
  }
}

api.interceptors.request.use((config) => {
  const token = useAuthStore.getState().accessToken;
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

/** Single-flight refresh: concurrent 401s share one refresh request. */
let refreshPromise: Promise<string | null> | null = null;

async function refreshAccessToken(): Promise<string | null> {
  refreshPromise ??= (async () => {
    try {
      const { refreshToken } = useAuthStore.getState();
      if (!refreshToken) {
        return null;
      }
      // Plain axios: the instance's interceptors must not recurse into refresh.
      const { data } = await axios.post<ApiResponse<{ accessToken: string; refreshToken: string }>>(
        '/api/auth/refresh',
        { refreshToken },
      );
      if (!data.success || !data.data) {
        return null;
      }
      useAuthStore.getState().setTokens(data.data.accessToken, data.data.refreshToken);
      return data.data.accessToken;
    } catch {
      return null;
    } finally {
      refreshPromise = null;
    }
  })();
  return refreshPromise;
}

interface RetriableConfig extends AxiosRequestConfig {
  _retried?: boolean;
}

api.interceptors.response.use(
  (response) => response,
  async (error: AxiosError<ApiResponse>) => {
    const config = error.config as RetriableConfig | undefined;
    const status = error.response?.status ?? null;

    const isAuthEndpoint = config?.url?.startsWith('/auth/') ?? false;
    if (status === 401 && config && !config._retried && !isAuthEndpoint) {
      config._retried = true;
      const newToken = await refreshAccessToken();
      if (newToken) {
        config.headers = { ...config.headers, Authorization: `Bearer ${newToken}` };
        return api.request(config);
      }
      useAuthStore.getState().clearSession();
    }

    throw new ApiClientError(error.response?.data ?? null, status);
  },
);

/** Unwraps the envelope; throws ApiClientError when success is false. */
export async function unwrap<T>(request: Promise<{ data: ApiResponse<T> }>): Promise<T> {
  const { data } = await request;
  if (!data.success || data.data === null) {
    throw new ApiClientError(data, null);
  }
  return data.data;
}
