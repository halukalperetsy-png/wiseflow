import type { ApiProblem } from './apiClient.ts';

/**
 * Error codes are stable English identifiers; the words a person reads live
 * here. An unknown code falls back to the server's own Turkish title, and only
 * then to a generic line -- so a new code never shows a blank message.
 */
const MESSAGES: Record<string, string> = {
  invalid_credentials: 'E-posta veya parola hatalı.',
  unauthorized: 'Oturumunuzun süresi doldu. Lütfen tekrar giriş yapın.',
  forbidden: 'Bu işlem için yetkiniz yok.',
  not_found: 'Kayıt bulunamadı.',
  validation_failed: 'Girdiğiniz bilgilerde hata var.',
  csrf_failed: 'Oturum doğrulaması başarısız. Sayfayı yenileyip tekrar deneyin.',
  password_change_required: 'Devam etmeden önce parolanızı değiştirmeniz gerekiyor.',
  email_already_exists: 'Bu e-posta adresi zaten kayıtlı.',
  product_group_code_already_exists: 'Bu kod başka bir ürün grubunda kullanılıyor.',
  self_role_change_forbidden: 'Kendi yetkilerinizi değiştiremezsiniz.',
  self_deactivation_forbidden: 'Kendi hesabınızı pasifleştiremezsiniz.',
  last_admin_protected: 'Sistemde en az bir aktif yönetici kalmalıdır.',
  too_many_requests: 'Çok fazla deneme yapıldı. Lütfen biraz bekleyip tekrar deneyin.',
  unreachable: 'Sunucuya ulaşılamadı. Bağlantınızı kontrol edin.',
  invalid_response: 'Sunucudan beklenmeyen bir yanıt geldi.',
  conflict: 'İşlem tamamlanamadı.',
  server_error: 'Beklenmeyen bir hata oluştu.',
};

export function describeProblem(problem: ApiProblem): string {
  return MESSAGES[problem.code] ?? (problem.title !== '' ? problem.title : MESSAGES['server_error']!);
}

/** Field-level messages for a form, empty when the failure was not a validation one. */
export function fieldErrors(problem: ApiProblem | null): Record<string, string[]> {
  return problem?.errors ?? {};
}

export function firstFieldError(problem: ApiProblem | null, field: string): string | undefined {
  return problem?.errors?.[field]?.[0];
}
