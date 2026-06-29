import { useParams, Link } from 'react-router-dom';
import { useInvoice } from '../../hooks/useInvoice';
import styles from './InvoiceDetailPage.module.css';

export function InvoiceDetailPage() {
  const { clientId, invoiceId } = useParams<{ clientId: string; invoiceId: string }>();
  const { invoice, loading, error } = useInvoice(invoiceId ?? '');

  if (loading) return <div className={styles.state}>Loading…</div>;
  if (error || !invoice) return <div className={styles.stateError}>{error ?? 'Invoice not found.'}</div>;

  return (
    <div className={styles.container}>
      <Link to={`/clients/${clientId}/invoices`} className={styles.back}>← Back to Invoices</Link>

      <div className={styles.header}>
        <div>
          <h1 className={styles.invoiceNumber}>{invoice.invoiceNumber}</h1>
          <p className={styles.client}>{invoice.clientName}</p>
        </div>
        <span className={`${styles.badge} ${styles[`badge${invoice.status}`]}`}>{invoice.status}</span>
      </div>

      <div className={styles.meta}>
        <div><span className={styles.label}>Issued</span>{new Date(invoice.issuedDate).toLocaleDateString()}</div>
        <div><span className={styles.label}>Due</span>{new Date(invoice.dueDate).toLocaleDateString()}</div>
        {invoice.paidAt && <div><span className={styles.label}>Paid</span>{new Date(invoice.paidAt).toLocaleDateString()}</div>}
      </div>

      {invoice.notes && <p className={styles.notes}>{invoice.notes}</p>}

      <table className={styles.table}>
        <thead>
          <tr>
            <th>Description</th>
            <th>Type</th>
            <th className={styles.right}>Rate</th>
            <th className={styles.right}>Qty</th>
            <th className={styles.right}>Total</th>
          </tr>
        </thead>
        <tbody>
          {invoice.lineItems
            .sort((a, b) => a.sortOrder - b.sortOrder)
            .map(li => (
              <tr key={li.id}>
                <td>{li.description}</td>
                <td><span className={`${styles.typeBadge} ${li.lineType === 'Cargo' ? styles.cargo : styles.passenger}`}>{li.lineType === 'PassengerService' ? 'Passenger' : 'Cargo'}</span></td>
                <td className={styles.right}>${li.unitRate.toFixed(2)}</td>
                <td className={styles.right}>{li.quantity}</td>
                <td className={styles.right}>${li.lineTotal.toFixed(2)}</td>
              </tr>
            ))}
        </tbody>
        <tfoot>
          <tr className={styles.totalRow}>
            <td colSpan={4} className={styles.totalLabel}>Total</td>
            <td className={styles.right}>${invoice.totalAmount.toFixed(2)}</td>
          </tr>
        </tfoot>
      </table>
    </div>
  );
}
