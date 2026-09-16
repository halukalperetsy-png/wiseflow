export interface ProductGroup {
  id: string;
  code: string;
  name: string;
  description: string | null;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface ProductGroupDraft {
  code: string;
  name: string;
  description: string;
}
