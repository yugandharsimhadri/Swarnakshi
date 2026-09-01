export type Role = 1 | 2 | 3 | 4; // Owner, SubOwner, Supervisor, Accountant
export const RoleName: Record<Role, string> = { 1: "Owner", 2: "Sub-Owner", 3: "Supervisor", 4: "Accountant" };

export interface AuthUser {
  id: string;
  name: string;
  username: string;
  /** username@companycode — what the person actually types to sign in. */
  login: string;
  email?: string | null;
  role: Role;
  isCompanyAdmin: boolean;
  permissions: string[];
}

export interface CompanyInfo {
  id: string;
  code: string;
  name: string;
  licenseExpiresOn: string;
  daysToExpiry: number;
  isActive: boolean;
}

export interface PlatformUserInfo { id: string; username: string; displayName: string }

export interface CompanyAdmin {
  userId: string; name: string; username: string; login: string; email?: string | null; isActive: boolean;
}

export interface CompanyOverview {
  id: string; code: string; name: string;
  contactEmail?: string | null; contactMobile?: string | null;
  licenseExpiresOn: string; daysToExpiry: number; isExpired: boolean; isActive: boolean;
  createdAt: string; userCount: number; siteCount: number; projectCount: number;
  admins: CompanyAdmin[];
}

export interface AdminUser {
  id: string;
  name: string;
  username: string;
  login: string;
  email?: string | null;
  role: Role;
  isActive: boolean;
  isCompanyAdmin: boolean;
  extraPermissions: string[];
  siteIds: string[];
}

export interface AuthResponse {
  kind: 'tenant' | 'platform';
  accessToken: string;
  accessTokenExpiresAt: string;
  refreshToken: string;
  refreshTokenExpiresAt: string;
  user: AuthUser | null;
  company: CompanyInfo | null;
  platformUser: PlatformUserInfo | null;
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
  completionPercent: number;
  notes?: string | null;
}

/** Counts by stage across the book of work. Cancelled is reported apart from the buckets. */
export interface ProjectProgress {
  total: number;
  notStarted: number;
  inProgress: number;
  completed: number;
  onHold: number;
  cancelled: number;
  averageCompletionOfInProgress: number;
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

export type SpecFieldKind = 1 | 2 | 3; // Text, Number, Select

export interface SpecDefinition {
  id: string;
  materialSubcategoryId: string;
  key: string;
  label: string;
  kind: SpecFieldKind;
  options: string[];
  isRequired: boolean;
  partOfIdentity: boolean;
  sortOrder: number;
}

export interface MaterialSpec {
  definitionId: string;
  key: string;
  label: string;
  value: string;
  sortOrder: number;
}

/** Row shape of the Material Master list. */
export interface Material {
  id: string;
  code: string;
  name: string;
  brand?: string | null;
  materialSubcategoryId: string;
  subcategoryName: string;
  materialCategoryId: string;
  categoryName: string;
  specSummary?: string | null;
  unitId: string;
  unitCode: string;
  defaultPurchaseRate: number;
  gstRate?: number | null;
  isActive: boolean;
}

/** Full record behind View / Edit. */
export interface MaterialDetail extends Material {
  specifications: MaterialSpec[];
  secondaryUnitId?: string | null;
  secondaryUnitCode?: string | null;
  conversionFactor?: number | null;
  genericMeasurement?: string | null;
  minStockLevel: number;
  reorderLevel: number;
  description?: string | null;
  notes?: string | null;
  codeLocked: boolean;
  hasStock: boolean;
  totalStock: number;
}

export interface MaterialSiteStock {
  siteId: string;
  siteName: string;
  quantity: number;
  averageRate: number;
  value: number;
}

export interface MaterialSummary {
  total: number;
  active: number;
  inactive: number;
  categories: number;
}

export interface SaveMaterialBody {
  code: string;
  name: string;
  materialSubcategoryId: string;
  brand?: string | null;
  unitId: string;
  secondaryUnitId?: string | null;
  conversionFactor?: number | null;
  genericMeasurement?: string | null;
  minStockLevel: number;
  reorderLevel: number;
  defaultPurchaseRate: number;
  gstRate?: number | null;
  description?: string | null;
  notes?: string | null;
  specifications: Record<string, string | null>;
}

export interface Subcategory { id: string; parentId: string; parentName: string; name: string; isActive: boolean }
export interface Category { id: string; name: string; sortOrder: number; isActive: boolean }

export const SiteStatusName: Record<number, string> = { 0: "Planned", 1: "Active", 2: "On Hold", 3: "Completed", 4: "Cancelled" };
export const ProjectStatusName = SiteStatusName;

export interface Lookup { id: string; name: string; isActive: boolean }

export interface KpiCard { label: string; value: number; format: "money" | "count" }
export interface RecentTxn { type: string; ref: string; date: string; amount: number; context?: string | null }
export interface DashboardPayload {
  role: string; kpis: KpiCard[]; recent: RecentTxn[]; pendingApprovals: number;
}
export interface ReportTable { title: string; columns: string[]; rows: (string | number | null)[][] }
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
  /** Set when this line was bought for one villa and taken straight there. */
  deliverToProjectId?: string | null;
  deliverToProjectName?: string | null;
  expenseHeadId?: string | null;
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

/** Row shape shared by the contractor / customer / supplier master lists. */
export interface Party {
  id: string;
  code: string;
  name: string;
  companyName?: string | null;
  mobile?: string | null;
  email?: string | null;
  gstin?: string | null;
  type?: string | null;
  isActive: boolean;
}

/** How many transactions reference this party — drives the code lock and the detail view. */
export interface PartyUsage {
  contracts: number;
  contractorPayments: number;
  projects: number;
  customerPayments: number;
  purchases: number;
  total: number;
}

export interface PartyDetail extends Party {
  address?: string | null;
  pan?: string | null;
  bankDetails?: string | null;
  notes?: string | null;
  codeLocked: boolean;
  usage: PartyUsage;
}

export interface PartySummary { total: number; active: number; inactive: number }

export interface SavePartyBody {
  code: string;
  name: string;
  companyName?: string | null;
  mobile?: string | null;
  email?: string | null;
  address?: string | null;
  pan?: string | null;
  gstin?: string | null;
  bankDetails?: string | null;
  type?: string | null;
  notes?: string | null;
}

/** Kept as aliases so existing pickers (ProjectDetail, Purchases) keep compiling. */
export type Contractor = Party;

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

export type Customer = Party;

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

// ---- Employees ---------------------------------------------------------
export interface Employee {
  id: string; code: string; name: string; phone: string; monthlySalary: number;
  joinDate: string; leaveDate?: string | null; designation?: string | null;
  address?: string | null; notes?: string | null;
  siteId?: string | null; siteName?: string | null; isActive: boolean;
  totalPaid: number; advanceOutstanding: number;
}

export interface EmployeePayment {
  id: string; txnNumber: string; employeeId: string; employeeName: string;
  date: string; kind: number; amount: number; advanceRecovered: number; netPaid: number;
  periodStart?: string | null; periodEnd?: string | null;
  paymentMethodId?: string | null; paymentMethodName?: string | null;
  reference?: string | null; projectId?: string | null; projectName?: string | null;
  status: number; remarks?: string | null;
}

export const EmployeePaymentKindName: Record<number, string> = {
  1: "Salary", 2: "Advance", 3: "Bonus", 4: "Reimbursement",
};

export interface EmployeeLedger {
  employeeId: string; employeeName: string; phone: string; monthlySalary: number;
  totalPaid: number; advancesGiven: number; advancesRecovered: number; advanceOutstanding: number;
  rows: { kind: string; ref: string; date: string; amount: number; advanceRecovered: number; netPaid: number; status: string }[];
}
