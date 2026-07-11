export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface ThreadDto {
  id: number;
  patientId: number;
  patientName: string;
  patientOptedOut: boolean;
  assignedStaffId: number | null;
  assignedStaffUsername: string | null;
  unreadCount: number;
}

export interface ThreadMessageDto {
  direction: "inbound" | "outbound";
  senderType: "System" | "Staff" | null;
  body: string;
  timestamp: string;
  status: string | null;
}

export interface ThreadDetailDto extends ThreadDto {
  messages: ThreadMessageDto[];
}
