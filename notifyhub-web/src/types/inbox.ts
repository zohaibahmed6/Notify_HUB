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
  // FR-010: paginated, not the thread's full message history. Page 1 = the most recent
  // `pageSize` messages; higher page numbers page backward into older history.
  messages: PagedResult<ThreadMessageDto>;
}
