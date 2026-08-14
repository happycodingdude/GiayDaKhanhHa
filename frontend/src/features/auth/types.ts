/** Type ở frontend mô tả hợp đồng API, không phải entity database (Step 5 §27). */

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
