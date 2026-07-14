export type UserRole = "Admin" | "Staff";
export type UserStatus = "Active" | "Inactive" | "OnLeave";

export interface UserDto {
  id: number;
  username: string;
  fullName: string | null;
  role: UserRole;
  status: UserStatus;
  leaveFrom: string | null;
  leaveTo: string | null;
}

export interface CreateUserRequest {
  username: string;
  fullName?: string;
  password: string;
  role: UserRole;
}

export interface UpdateUserStatusRequest {
  status: UserStatus;
  leaveFrom?: string;
  leaveTo?: string;
}
