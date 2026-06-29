import { useState, useEffect } from 'react';
import apiClient from '../api/client';
import type { InvoiceDetail } from '../types/invoice';

export function useInvoice(invoiceId: string) {
  const [invoice, setInvoice] = useState<InvoiceDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!invoiceId) return;
    setLoading(true);
    apiClient.get<InvoiceDetail>(`/api/invoices/${invoiceId}`)
      .then(res => setInvoice(res.data))
      .catch(() => setError('Failed to load invoice'))
      .finally(() => setLoading(false));
  }, [invoiceId]);

  return { invoice, loading, error };
}
