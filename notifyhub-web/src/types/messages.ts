export type MessageStatus = "Queued" | "Sending" | "Sent" | "Delivered" | "Failed" | "Superseded" | "Expired";

export interface SmsHistoryDto {
  id: number;
  patientName: string;
  senderUsername: string;
  phone: string;
  text: string | null;
  status: MessageStatus;
  scheduledTime: string;
  expiryTime: string | null;
  pduCount: number | null;
}

export interface SmsHistoryPagedResult {
  items: SmsHistoryDto[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPduCount: number;
}
