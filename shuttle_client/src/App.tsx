import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { ClientInvoicesPage } from './pages/clients/ClientInvoicesPage';
import { InvoiceDetailPage } from './pages/clients/InvoiceDetailPage';

export default function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/clients/:clientId/invoices" element={<ClientInvoicesPage />} />
        <Route path="/clients/:clientId/invoices/:invoiceId" element={<InvoiceDetailPage />} />
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </BrowserRouter>
  );
}
