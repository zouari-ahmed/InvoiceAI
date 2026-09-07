import type { ReactNode } from "react";
import { Navigate, Route, Routes } from "react-router-dom";
import { getToken } from "./api";
import Login from "./pages/Login";
import Invoices from "./pages/Invoices";
import Review from "./pages/Review";
import Upload from "./pages/Upload";

function RequireAuth({ children }: { children: ReactNode }) {
  return getToken() ? <>{children}</> : <Navigate to="/login" replace />;
}

export default function App() {
  return (
    <Routes>
      <Route path="/login" element={<Login />} />
      <Route path="/" element={<RequireAuth><Invoices /></RequireAuth>} />
      <Route path="/upload" element={<RequireAuth><Upload /></RequireAuth>} />
      <Route path="/invoices/:id" element={<RequireAuth><Review /></RequireAuth>} />
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}