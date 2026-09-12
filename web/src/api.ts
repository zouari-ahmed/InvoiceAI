import type { InvoiceSummary, InvoiceDetail, UploadResult } from "./types";

const BASE = "https://localhost:7007/api";

export function getToken(): string | null {
  return localStorage.getItem("invoiceai_token");
}

export function setToken(token: string | null) {
  if (token) localStorage.setItem("invoiceai_token", token);
  else localStorage.removeItem("invoiceai_token");
}

async function request<T>(path: string, init: RequestInit = {}): Promise<T> {
  const token = getToken();

  const headers = new Headers(init.headers);
  if (token) headers.set("Authorization", `Bearer ${token}`);
  if (init.body && !(init.body instanceof FormData)) {
    headers.set("Content-Type", "application/json");
  }

  const response = await fetch(`${BASE}${path}`, { ...init, headers });

  if (response.status === 401) {
    setToken(null);
    throw new Error("Session expired. Please log in again.");
  }

  if (!response.ok) {
    const text = await response.text();
    throw new Error(text || `Request failed with status ${response.status}`);
  }

  if (response.status === 204) return undefined as T;

  return (await response.json()) as T;
}
export async function login(email: string, password: string) {
  const data = await request<{ token: string; email: string; roles: string[] }>(
    "/auth/login",
    { method: "POST", body: JSON.stringify({ email, password }) }
  );
  setToken(data.token);
  return data;
}

export function listInvoices() {
  return request<InvoiceSummary[]>("/invoices");
}

export function getInvoice(id: number) {
  return request<InvoiceDetail>(`/invoices/${id}/details`);
}

export function updateInvoice(id: number, body: unknown) {
  return request<InvoiceDetail>(`/invoices/${id}`, {
    method: "PUT",
    body: JSON.stringify(body),
  });
}

export function extractInvoice(id: number) {
  return request<InvoiceSummary>(`/invoices/${id}/extract`, { method: "POST" });
}

export function uploadFiles(files: File[]) {
  const form = new FormData();
  files.forEach((f) => form.append("files", f));
  return request<UploadResult>("/invoices/upload", {
    method: "POST",
    body: form,
  });
}

export async function fetchFileBlobUrl(id: number): Promise<string> {
  const token = getToken();

  const response = await fetch(`${BASE}/invoices/${id}/file`, {
    headers: token ? { Authorization: `Bearer ${token}` } : {},
  });

  if (!response.ok) {
    throw new Error("Could not load the source document.");
  }

  const blob = await response.blob();
  return URL.createObjectURL(blob);
}
export function getStats() {
  return request<import("./types").DashboardStats>("/stats");
}