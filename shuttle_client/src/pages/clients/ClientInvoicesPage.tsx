import { useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { useClientInvoices } from '../../hooks/useClientInvoices';
import styles from './ClientInvoicesPage.module.css';

export function ClientInvoicesPage() {
  const { clientId } = useParams<{ clientId: string }>();
  const [statusFilter, setStatusFilter] = useState('');
  const { invoices, loading, error } = useClientInvoices(clientId ?? '', statusFilter || undefined);

  if (loading) return <div className={styles.state}>Loading invoices…</div>;
  if (error) return <div className={styles.stateError}>{error}</div>;

  return (
    <div className={styles.container}>
      <div className={styles.header}>
        <h1 className={styles.title}>Invoices</h1>
        <select
          className={styles.filter}
          value={statusFilter}
          onChange={e => setStatusFilter(e.target.value)}
        >
          <option value="">All statuses</option>
          <option value="Draft">Draft</option>
          <option value="Sent">Sent</option>
          <option value="Paid">Paid</option>
          <option value="Overdue">Overdue</option>
          <option value="Void">Void</option>
        </select>
      </div>

      {invoices.length === 0 ? (
        <div className={styles.state}>No invoices found.</div>
      ) : (
        <table className={styles.table}>
          <thead>
            <tr>
              <th>Invoice #</th>
              <th>Issued</th>
              <th>Due</th>
              <th>Status</th>
              <th>Total</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {invoices.map(inv => (
              <tr key={inv.id}>
                <td>{inv.invoiceNumber}</td>
                <td>{new Date(inv.issuedDate).toLocaleDateString()}</td>
                <td>{new Date(inv.dueDate).toLocaleDateString()}</td>
                <td><span className={`${styles.badge} ${styles[`badge${inv.status}`]}`}>{inv.status}</span></td>
                <td className={styles.amount}>${inv.totalAmount.toFixed(2)}</td>
                <td><Link to={`/clients/${clientId}/invoices/${inv.id}`} className={styles.viewLink}>View</Link></td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  );
}
