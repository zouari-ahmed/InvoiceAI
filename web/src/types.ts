export type InvoiceSummary = {
  id: number;
  invoiceNumber: string | null;
  issueDate: string | null;
  supplierName: string | null;
  total: number;
  currency: string;
  status: string;
};

export type Party = {
  id: number;
  name: string;
  address: string | null;
  taxId: string | null;
};

export type LineItem = {
  id: number;
  description: string;
  quantity: number;
  unitPrice: number;
  vatRate: number | null;
  lineTotal: number | null;
};

export type InvoiceDetail = {
  id: number;
  invoiceNumber: string | null;
  issueDate: string | null;
  dueDate: string | null;
  supplier: Party | null;
  customer: Party | null;
  subtotal: number;
  vatAmount: number;
  total: number;
  currency: string;
  status: string;
  originalFileName: string | null;
  contentType: string | null;
  createdAt: string | null;
  updatedAt: string | null;
  lineItems: LineItem[];
};

export type UploadResult = {
  accepted: { invoiceId: number; fileName: string; sizeBytes: number }[];
  rejected: { fileName: string; reason: string }[];
};
export type StatusCount = { status: string; count: number };

export type SupplierStat = { supplier: string; count: number; total: number };

export type DashboardStats = {
  totalInvoices: number;
  totalAmount: number;
  currency: string;
  byStatus: StatusCount[];
  topSuppliers: SupplierStat[];
  accuracySampleSize: number;
  fieldsCompared: number;
  fieldsCorrect: number;
  accuracyRate: number | null;
};