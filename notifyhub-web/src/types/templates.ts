export type TemplateTriggerType = "AppointmentReminder" | "MedicationAlert" | "PrescriptionAlert";

export interface TemplateDto {
  id: number;
  name: string;
  body: string;
  triggerType: TemplateTriggerType;
  offsetHours: number;
}

export interface CreateTemplateRequest {
  name: string;
  body: string;
  triggerType: TemplateTriggerType;
  offsetHours: number;
}

export interface UpdateTemplateRequest {
  name?: string;
  body?: string;
  triggerType?: TemplateTriggerType;
  offsetHours?: number;
}
