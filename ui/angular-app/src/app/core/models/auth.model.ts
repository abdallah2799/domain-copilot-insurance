// Mirrors AuthController's DTOs (AuthController.LoginRequest / Application.Identity.LoginResult).

export type UserRole = 'Analyst' | 'Adjuster';

export interface LoginRequest {
  username: string;
  password: string;
}

export interface LoginResult {
  token: string;
  username: string;
  role: UserRole;
}
