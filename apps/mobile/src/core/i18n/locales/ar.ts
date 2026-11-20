import type { Resources } from './en';

type Widen<T> = { [K in keyof T]: T[K] extends string ? string : Widen<T[K]> };

export const ar: Widen<Resources> = {
  app: {
    name: 'كونستركشن كونكت',
  },
  nav: {
    home: 'الرئيسية',
    projects: 'المشاريع',
    tasks: 'المهام',
    approvals: 'الموافقات',
    create: 'إنشاء',
    procurement: 'المشتريات',
    finance: 'المالية',
    reports: 'التقارير',
    settings: 'الإعدادات',
  },
  scope: {
    company: 'الشركة',
    project: 'المشروع',
    mine: 'أنا',
  },
  status: {
    onTrack: 'على المسار',
    inProgress: 'قيد التنفيذ',
    atRisk: 'في خطر',
    blocked: 'متوقف',
    done: 'مكتمل',
    draft: 'مسودة',
    awaiting: 'بانتظار قرار من',
  },
  action: {
    approve: 'اعتماد',
    reject: 'رفض',
    saveDraft: 'حفظ مسودة',
    send: 'إرسال',
    cancel: 'إلغاء',
    back: 'رجوع',
    seeAll: 'عرض الكل',
    retry: 'إعادة المحاولة',
    undo: 'تراجع',
    dismiss: 'إغلاق',
    report: 'تقرير',
    request: 'طلب',
  },
  sync: {
    synced: 'متزامن',
    offline: 'بدون إنترنت',
    offlineQueued: 'بدون إنترنت، {{count}} تغييرات في الانتظار',
  },
};
