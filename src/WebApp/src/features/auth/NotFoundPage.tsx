import { Link } from 'react-router';
import { EmptyState, PageHeader } from '../../shared/ui.tsx';

export function NotFoundPage() {
  return (
    <div className="co-page">
      <PageHeader title="Sayfa bulunamadı" />
      <EmptyState
        title="Aradığınız sayfa yok."
        hint="Bağlantı hatalı olabilir veya sayfa kaldırılmış olabilir."
      />
      <p style={{ margin: 0 }}>
        <Link className="co-link" to="/product-groups">
          Ürün gruplarına dön
        </Link>
      </p>
    </div>
  );
}
