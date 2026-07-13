export interface BookmarkDto {
  id: number;
  label: string;
  description: string;
  insertText: string;
}

export interface CreateBookmarkRequest {
  label: string;
  description: string;
  insertText: string;
}

export interface UpdateBookmarkRequest {
  label?: string;
  description?: string;
  insertText?: string;
}
