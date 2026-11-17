import type { FileKind, Module } from "@/features/tasks/types";
import type { Locale } from "@/i18n/locales";

type Text = Record<Locale, string>;

export type TagKey =
  | "procurement"
  | "receiving"
  | "matching"
  | "draft"
  | "site"
  | "rfi"
  | "payment";
export type TagTone =
  | "primary"
  | "ok"
  | "warn"
  | "danger"
  | "info"
  | "ai"
  | "neutral";
export type Due = "today" | "tomorrow" | "overdue" | `${number}:${number}`;

export type MockTask = {
  id: string;
  code: string;
  module: Module;
  title: Text;
  meta?: Text;
  description: Text;
  tag: { key: TagKey; tone: TagTone };
  progress?: number;
  due: Due;
  attachments: { name: string; sizeKb: number; kind: FileKind }[];
  comments: { author: Text; text: Text; when: Text }[];
};

const sami: Text = { ar: "م. سامي", en: "Eng. Sami" };
const rana: Text = { ar: "رنا", en: "Rana" };
const hesham: Text = { ar: "أ. هشام", en: "Mr. Hesham" };

export const mockTasks = [
  {
    id: "po-1071",
    code: "PO-1071",
    module: "procurement",
    title: { ar: "اعتمد أمر الشراء", en: "Approve the purchase order" },
    meta: {
      ar: "أسمنت وزلط · 45,000 ج.م",
      en: "Cement and gravel · EGP 45,000",
    },
    description: {
      ar: "أمر شراء الأسمنت والزلط للدور الثالث. السعر أقل من آخر توريد بنسبة 3%، والمورد معتمد. يلزم اعتماد مدير المشروع قبل نهاية اليوم حتى يصل التوريد في موعده.",
      en: "Purchase order for the floor 3 cement and gravel. The price is 3% below the last delivery and the supplier is approved. The project manager must approve before end of day so the delivery arrives on time.",
    },
    tag: { key: "procurement", tone: "ai" },
    due: "today",
    attachments: [
      { name: "PO-1071.pdf", sizeKb: 214, kind: "file" },
      { name: "supplier-quote.pdf", sizeKb: 380, kind: "file" },
      { name: "price-comparison.xlsx", sizeKb: 96, kind: "sheet" },
    ],
    comments: [
      {
        author: rana,
        text: {
          ar: "قارنت العرض مع آخر ثلاثة موردين وهذا الأفضل.",
          en: "I compared the quote with the last three suppliers and this is the best.",
        },
        when: { ar: "أمس 16:20", en: "Yesterday 16:20" },
      },
      {
        author: sami,
        text: {
          ar: "تمام من ناحية الكميات، بانتظار اعتمادك.",
          en: "Quantities are fine, waiting for your approval.",
        },
        when: { ar: "اليوم 08:05", en: "Today 08:05" },
      },
    ],
  },
  {
    id: "grn-0412",
    code: "GRN-0412",
    module: "site",
    title: { ar: "استلم توريد الطوب", en: "Receive the brick delivery" },
    meta: { ar: "PO-1066 · الموقع 11:00", en: "PO-1066 · Site 11:00" },
    description: {
      ar: "وصول شاحنة الطوب الأحمر للدور الثاني. راجع العدد والجودة مقابل أمر الشراء، وصوّر أي كسر قبل التوقيع على إذن الاستلام.",
      en: "The red brick truck arrives for floor 2. Check the count and quality against the purchase order, and photograph any breakage before signing the receipt.",
    },
    tag: { key: "receiving", tone: "info" },
    due: "11:00",
    attachments: [{ name: "PO-1066.pdf", sizeKb: 188, kind: "file" }],
    comments: [],
  },
  {
    id: "inv-2231",
    code: "INV-2231",
    module: "money",
    title: {
      ar: "راجع فارق فاتورة الزلط",
      en: "Review the gravel invoice variance",
    },
    meta: { ar: "نقص 2 م³ · 900 ج.م", en: "Short by 2 m³ · EGP 900" },
    description: {
      ar: "الفاتورة تشمل 42 م³ بينما استُلم 40 م³ فقط حسب إذن الاستلام. الفارق 900 ج.م. اطلب من المورد إشعار خصم أو أرسل الكمية الناقصة.",
      en: "The invoice bills 42 m³ but only 40 m³ was received according to the receipt. The difference is EGP 900. Ask the supplier for a credit note or the missing quantity.",
    },
    tag: { key: "matching", tone: "warn" },
    due: "tomorrow",
    attachments: [
      { name: "INV-2231.pdf", sizeKb: 301, kind: "file" },
      { name: "GRN-0398.pdf", sizeKb: 175, kind: "file" },
    ],
    comments: [
      {
        author: hesham,
        text: {
          ar: "راجعت إذن الاستلام وفعلاً الكمية 40.",
          en: "I checked the receipt and the quantity is indeed 40.",
        },
        when: { ar: "أمس 13:40", en: "Yesterday 13:40" },
      },
    ],
  },
  {
    id: "sr-0090",
    code: "SR-0090",
    module: "procurement",
    title: {
      ar: "راجع مسودة طلب الحديد",
      en: "Review the draft steel request",
    },
    meta: {
      ar: "صاغها المساعد من تسجيل صوتي",
      en: "Drafted by the assistant from a voice note",
    },
    description: {
      ar: "مسودة طلب شراء حديد تسليح صاغها المساعد من تسجيل صوتي لمهندس الموقع. راجع الأقطار والكميات قبل تحويلها إلى طلب رسمي.",
      en: "A draft steel reinforcement request the assistant wrote from the site engineer's voice note. Check the diameters and quantities before turning it into a formal request.",
    },
    tag: { key: "draft", tone: "primary" },
    due: "tomorrow",
    attachments: [{ name: "site-voice-note.m4a", sizeKb: 1240, kind: "file" }],
    comments: [],
  },
  {
    id: "a-198",
    code: "A-198",
    module: "site",
    title: {
      ar: "تحديث تقدم محارة الدور 2",
      en: "Update floor 2 plastering progress",
    },
    description: {
      ar: "حدّث نسبة إنجاز المحارة للدور الثاني بعد المعاينة الصباحية، وأرفق صور الأوجه المنتهية.",
      en: "Update the floor 2 plastering completion after the morning inspection and attach photos of the finished faces.",
    },
    tag: { key: "site", tone: "neutral" },
    progress: 72,
    due: "today",
    attachments: [
      { name: "floor2-east.jpg", sizeKb: 1820, kind: "image" },
      { name: "floor2-west.jpg", sizeKb: 1640, kind: "image" },
    ],
    comments: [],
  },
  {
    id: "rfi-031",
    code: "RFI-031",
    module: "quality",
    title: {
      ar: "ردّ الاستشاري على تعارض الدكت",
      en: "Consultant reply on the duct clash",
    },
    meta: { ar: "يعطّل أعمال المحارة", en: "Blocks the plastering works" },
    description: {
      ar: "تعارض بين مسار الدكت وكمرة الدور الثاني. أُرسل الاستفسار للاستشاري ولم يصل رد، والأعمال المتأثرة متوقفة.",
      en: "The duct route clashes with the floor 2 beam. The query went to the consultant with no reply yet, and the affected works are on hold.",
    },
    tag: { key: "rfi", tone: "danger" },
    due: "overdue",
    attachments: [
      { name: "duct-clash-drawing.pdf", sizeKb: 2210, kind: "file" },
    ],
    comments: [
      {
        author: sami,
        text: {
          ar: "ذكّرت الاستشاري هاتفيًا ووعد بالرد اليوم.",
          en: "I reminded the consultant by phone and he promised a reply today.",
        },
        when: { ar: "أمس 15:10", en: "Yesterday 15:10" },
      },
    ],
  },
  {
    id: "pa-04",
    code: "PA-04",
    module: "money",
    title: {
      ar: "اعتماد العميل للمستخلص",
      en: "Client approval of the payment certificate",
    },
    meta: { ar: "2,352,590 ج.م", en: "EGP 2,352,590" },
    description: {
      ar: "المستخلص الرابع مرسل للعميل بانتظار الاعتماد، وصرفه يؤثر مباشرة على الموقف النقدي للشهر القادم.",
      en: "Payment certificate 4 is with the client awaiting approval. Its release directly affects next month's cash position.",
    },
    tag: { key: "payment", tone: "ok" },
    due: "tomorrow",
    attachments: [
      { name: "PA-04.pdf", sizeKb: 640, kind: "file" },
      { name: "quantities.xlsx", sizeKb: 112, kind: "sheet" },
    ],
    comments: [],
  },
  {
    id: "po-1070",
    code: "PO-1070",
    module: "procurement",
    title: { ar: "اعتمدت أمر الرمل", en: "Approved the sand order" },
    meta: { ar: "31,500 ج.م", en: "EGP 31,500" },
    description: {
      ar: "تم اعتماد أمر شراء الرمل وإرساله للمورد.",
      en: "The sand purchase order was approved and sent to the supplier.",
    },
    tag: { key: "procurement", tone: "ai" },
    due: "08:40",
    attachments: [{ name: "PO-1070.pdf", sizeKb: 205, kind: "file" }],
    comments: [],
  },
  {
    id: "sr-0087",
    code: "SR-0087",
    module: "procurement",
    title: { ar: "أرسلت طلب الأسمنت", en: "Sent the cement request" },
    description: {
      ar: "أُرسل طلب شراء الأسمنت لقسم المشتريات.",
      en: "The cement purchase request was sent to procurement.",
    },
    tag: { key: "procurement", tone: "ai" },
    due: "08:15",
    attachments: [],
    comments: [],
  },
] satisfies MockTask[];

export const mockBoard = {
  todo: ["po-1071", "grn-0412", "inv-2231", "sr-0090"],
  doing: ["a-198"],
  waiting: ["rfi-031", "pa-04"],
  done: ["po-1070", "sr-0087"],
};
