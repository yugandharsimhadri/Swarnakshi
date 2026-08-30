export type Role = 1 | 2 | 3 | 4; // Owner, SubOwner, Supervisor, Accountant
export const RoleName: Record<Role, string> = { 1: "Owner", 2: "Sub-Owner", 3: "Supervisor", 4: "Accountant" };

export interface AuthUser {
  id: string;
  name: string;
  email: string;
  role: Role;
  permissions: string[];
}

export interface AuthResponse {
  accessToken: string;
  accessTokenExpiresAt: string;
  refreshToken: string;
  refreshTokenExpiresAt: string;
  user: AuthUser;
}

export interface Paged<T> {
  items: T[];
  page: number;
  pageSize: number;
  total: number;
}

export interface Site {
  id: string;
  code: string;
  name: string;
  city?: string | null;
  state?: string | null;
  pin?: string | null;
  supervisorUserId?: string | null;
  startDate?: string | null;
  status: number;
  notes?: string | null;
  projectCount: number;
  inventoryValue: number;
}

export interface Project {
  id: string;
  code: string;
  name: string;
  villaNumber?: string | null;
  siteId: string;
  siteName: string;
  customerId?: string | null;
  customerName?: string | null;
  projectTypeId?: string | null;
  startDate?: string | null;
  expectedCompletionDate?: string | null;
  estimatedCost: number;
  contractSaleValue?: number | null;
  status: number;
  notes?: string | null;
}

export interface ProjectSummary {
  projectId: string;
  name: string;
  estimatedCost: number;
  contractSaleValue?: number | null;
  materialCost: number;
  labourCost: number;
  contractorCost: number;
  otherCost: number;
  totalCost: number;
  customerReceived: number;
  customerOutstanding: number;
  budgetVariance: number;
  margin?: number | null;
}

export interface Material {
  id: string;
  code: string;
  name: string;
  subcategoryName: string;
  categoryName: string;
  unitCode: string;
  defaultPurchaseRate: number;
  minStockLevel: number;
  reorderLevel: number;
  isActive: boolean;
}

export const SiteStatusName: Record<number, string> = { 0: "Planned", 1: "Active", 2: "On Hold", 3: "Completed", 4: "Cancelled" };
export const ProjectStatusName = SiteStatusName;
