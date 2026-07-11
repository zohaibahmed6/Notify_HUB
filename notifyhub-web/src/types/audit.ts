export interface AuditLogDto {
  id: number;
  actor: string;
  action: string;
  entityType: string;
  entityId: number;
  occurredAt: string;
  detail: string | null;
}
