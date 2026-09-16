import { Link } from 'react-router';
import { Alert, PageHeader } from '../../shared/ui.tsx';

export function ForbiddenPage() {
  return (
    <div className="co-page">
      <PageHeader title="Erişim reddedildi" />
      <Alert kind="error" title="Bu sayfayı görüntüleme yetkiniz yok.">
        <p style={{ margin: 0 }}>
          Bu işlem için gereken yetkiye sahip değilsiniz. Yetki gerektiğini düşünüyorsanız bir
          yöneticiyle görüşün.
        </p>
      </Alert>
      <p style={{ margin: 0 }}>
        <Link className="co-link" to="/product-groups">
          Ürün gruplarına dön
        </Link>
      </p>
    </div>
  );
}
