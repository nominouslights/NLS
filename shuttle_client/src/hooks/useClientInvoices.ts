import { useState, useEffect } from 'react';
import apiClient from '../api/client';
import type { InvoiceListItem } from '../types/invoice';

export function useClientInvoices(clientId: string, status?: string) {
  const [invoices, setInvoices] = useState<InvoiceListItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!clientId) return;
    setLoading(true);
    const params: Record<string, string> = { clientId };
    if (status) params.status = status;
    apiClient.get<InvoiceListItem[]>('/api/invoices', { params })
      .then(res => setInvoices(res.data))
      .catch(() => setError('Failed to load invoices'))
      .finally(() => setLoading(false));
  }, [clientId, status]);

  return { invoices, loading, error };
}
