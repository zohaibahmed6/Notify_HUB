export type CommunicationMode = "Sms" | "Email" | "Letter";

export interface TemplateDto {
  id: number;
  name: string;
  body: string;
  offsetHours: number;
  isActive: boolean;
  communicationMode: CommunicationMode;
  bookmarkIds: number[];
}

export interface CreateTemplateRequest {
  name: string;
  body: string;
  offsetHours: number;
  communicationMode?: CommunicationMode;
  bookmarkIds?: number[];
}

export interface UpdateTemplateRequest {
  name?: string;
  body?: string;
  offsetHours?: number;
  isActive?: boolean;
  communicationMode?: CommunicationMode;
  bookmarkIds?: number[];
}
