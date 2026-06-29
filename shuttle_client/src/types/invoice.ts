export interface InvoiceListItem {
  id: string;
  invoiceNumber: string;
  clientId: string;
  clientName: string;
  status: 'Draft' | 'Sent' | 'Paid' | 'Overdue' | 'Void';
  issuedDate: string;
  dueDate: string;
  totalAmount: number;
}

export interface InvoiceLineItem {
  id: string;
  tripId: string | null;
  lineType: 'PassengerService' | 'Cargo';
  description: string;
  unitRate: number;
  quantity: number;
  lineTotal: number;
  sortOrder: number;
}

export interface InvoiceDetail extends InvoiceListItem {
  notes: string | null;
  paidAt: string | null;
  subTotal: number;
  lineItems: InvoiceLineItem[];
}
