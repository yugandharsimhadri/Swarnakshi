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
  materialSubcategoryId: string;
  subcategoryName: string;
  categoryName: string;
  unitId: string;
  unitCode: string;
  defaultPurchaseRate: number;
  minStockLevel: number;
  reorderLevel: number;
  gstRate?: number | null;
  isActive: boolean;
  description?: string | null;
  notes?: string | null;
}

export const SiteStatusName: Record<number, string> = { 0: "Planned", 1: "Active", 2: "On Hold", 3: "Completed", 4: "Cancelled" };
export const ProjectStatusName = SiteStatusName;

export interface Lookup { id: string; name: string; isActive: boolean }
export interface Unit { id: string; code: string; name: string; isActive: boolean }

export interface InventoryBalance {
  materialId: string;
  materialCode: string;
  materialName: string;
  categoryName: string;
  unitCode: string;
  quantity: number;
  averageRate: number;
  value: number;
  minStockLevel: number;
  reorderLevel: number;
  lastPurchaseRate?: number | null;
  lowStock: boolean;
}

export interface InventoryTxn {
  id: string;
  txnNumber: string;
  date: string;
  type: number;
  materialName: string;
  unitCode: string;
  quantity: number;
  rate: number;
  amount: number;
  projectId?: string | null;
  projectName?: string | null;
  sourceType?: string | null;
  sourceRef?: string | null;
  remarks?: string | null;
}

export const InvTxnTypeName: Record<number, string> = {
  1: "Opening", 2: "Purchase", 3: "Transfer", 4: "Consumption", 5: "Adjustment",
  6: "Return", 7: "Wastage", 8: "Other In", 9: "Other Out",
};

export interface MaterialInventoryDetail {
  siteId: string;
  materialId: string;
  materialName: string;
  unitCode: string;
  quantity: number;
  averageRate: number;
  value: number;
  minStockLevel: number;
  lastPurchaseRate?: number | null;
  totalPurchasedQty: number;
  totalConsumedQty: number;
}

export const TxnStatusName: Record<number, string> = {
  0: "Draft", 1: "Submitted", 2: "Pending Approval", 3: "Approved",
  4: "Rejected", 5: "Cancelled", 6: "Posted",
};

export const MatReqStatusName: Record<number, string> = {
  0: "Draft", 1: "Submitted", 2: "Pending Approval", 3: "Approved", 4: "Rejected",
  5: "Issued", 6: "Partially Issued", 7: "Cancelled",
};

export interface Purchase {
  id: string;
  txnNumber: string;
  supplierId: string;
  supplierName: string;
  siteId: string;
  siteName: string;
  invoiceNumber?: string | null;
  date: string;
  subTotal: number;
  taxAmount: number;
  totalAmount: number;
  paidAmount: number;
  balanceAmount: number;
  paymentStatus: number;
  status: number;
  items: PurchaseItem[];
}

export interface PurchaseItem {
  id: string;
  materialId: string;
  materialName: string;
  unitCode: string;
  quantity: number;
  rate: number;
  discount: number;
  taxAmount: number;
  lineTotal: number;
}

export interface MaterialRequest {
  id: string;
  txnNumber: string;
  siteId: string;
  siteName: string;
  projectId: string;
  projectName: string;
  requestType: number;
  requestStatus: number;
  status: number;
  date: string;
  notes?: string | null;
  items: MaterialRequestItem[];
}

export interface MaterialRequestItem {
  id: string;
  materialId: string;
  materialName: string;
  unitCode: string;
  requestedQty: number;
  approvedQty?: number | null;
  issuedQty: number;
  expenseHeadId?: string | null;
  expenseSubheadId?: string | null;
}

export interface Contractor {
  id: string; code: string; name: string; companyName?: string | null;
  mobile?: string | null; email?: string | null; type?: string | null; isActive: boolean;
}

export interface ProjectExpense {
  id: string; txnNumber: string; projectId: string; date: string;
  expenseHeadId: string; expenseHeadName: string; expenseSubheadName?: string | null;
  description?: string | null; amount: number; expenseType: number; paymentStatus: number;
  sourceType?: string | null; status: number;
}
export const ExpenseTypeName: Record<number, string> = {
  1: "Material", 2: "Labour", 3: "Contractor", 4: "Direct", 5: "Transport", 6: "Machinery", 7: "Other",
};

export interface LabourEntry {
  id: string; txnNumber: string; projectId: string; projectName: string;
  labourCategoryId: string; labourCategoryName: string; periodType: number;
  periodStart: string; periodEnd: string; amount: number; paymentType?: string | null;
  remarks?: string | null; status: number;
}

export interface ContractWork {
  id: string; projectId: string; projectName: string; contractorId: string; contractorName: string;
  workCategory: string; description?: string | null; estimatedCost: number; contractAmount: number;
  startDate?: string | null; expectedCompletion?: string | null; workStatus: number;
  totalPaid: number; balance: number;
}
export const ContractStatusName: Record<number, string> = {
  0: "Planned", 1: "Active", 2: "Completed", 3: "Cancelled", 4: "On Hold",
};

export interface ContractorPayment {
  id: string; txnNumber: string; contractorId: string; contractorName: string;
  projectId: string; projectName: string; contractWorkId?: string | null;
  date: string; amount: number; paymentMethodName: string; referenceNumber?: string | null;
  description?: string | null; paymentKind: number; status: number;
}

export interface CostByHead { expenseHeadId: string; expenseHeadName: string; amount: number }

export interface Customer {
  id: string; code: string; name: string; mobile?: string | null; email?: string | null; isActive: boolean;
}

export interface CustomerPayment {
  id: string; txnNumber: string; projectId: string; projectName: string; customerId: string; customerName: string;
  date: string; amount: number; paymentMethodName: string; reference?: string | null;
  description?: string | null; status: number;
}

export interface CustomerLedger {
  customerId: string; customerName: string;
  totalSaleValue: number; totalReceived: number; outstanding: number;
  rows: { kind: string; ref: string; date: string; charged: number; received: number }[];
}

export interface ContractorSummary {
  contractorId: string; contractorName: string;
  totalContracted: number; totalPaid: number; outstanding: number;
  rows: { kind: string; ref: string; date: string; contracted: number; paid: number }[];
}

export interface ApprovalItem {
  id: string;
  entityType: string;
  entityId: string;
  entityRef?: string | null;
  siteId?: string | null;
  projectId?: string | null;
  amount?: number | null;
  status: number;
  requestedByUserId: string;
  requestedAt: string;
  remarks?: string | null;
}
