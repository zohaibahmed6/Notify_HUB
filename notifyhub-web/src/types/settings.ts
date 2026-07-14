export interface SettingsDto {
  quietHoursEnabled: boolean;
  quietHoursStart: string;
  quietHoursEnd: string;
  rateLimitEnabled: boolean;
  rateLimitMaxMessages: number;
  rateLimitWindowHours: number;
  reminderOffsetMinutes: number;
  reminderExpiryOffsetMinutes: number;
}

export interface UpdateSettingsRequest {
  quietHoursEnabled?: boolean;
  quietHoursStart?: string;
  quietHoursEnd?: string;
  rateLimitEnabled?: boolean;
  rateLimitMaxMessages?: number;
  rateLimitWindowHours?: number;
  reminderOffsetMinutes?: number;
  reminderExpiryOffsetMinutes?: number;
}

export interface SystemInfoDto {
  databaseConnected: boolean;
  dispatcherPollIntervalSeconds: number;
  escalationPollIntervalSeconds: number;
}
