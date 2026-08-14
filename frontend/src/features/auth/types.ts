/** Frontend types represent the API contract, not database entities (Step 5 §27). */

export interface CurrentUserDto {
  id: string
  username: string
  displayName: string
  status: string
}

export interface LoginRequest {
  username: string
  password: string
}
