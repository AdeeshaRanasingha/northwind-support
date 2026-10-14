import { request } from './client';

export interface LoginRequest {
  email: string;
  password: string;
}

export interface LoginResponse {
  accessToken: string;
  expiresAtUtc: string;
  agentId: number;
  fullName: string;
  email: string;
}

export interface CurrentAgent {
  id: number;
  fullName: string;
  email: string;
}

export const authApi = {
  me: (signal?: AbortSignal) => request<CurrentAgent>('/auth/me', { signal }),
  login: (payload: LoginRequest) => request<LoginResponse>('/auth/login', {
    method: 'POST',
    body: JSON.stringify(payload),
  }),
};
