namespace Construction.Platform.Contracts;

/// <summary>
/// Platform module error codes (Notion 01 §12) with bilingual (English and Arabic) messages.
/// Every code carries the <c>platform.</c> prefix.
/// </summary>
public static class PlatformErrorCodes
{
    // Outbox / dead letters (PLT-204)
    public const string DeadMessageNotFound = "platform.dead_message_not_found";
    public const string DeadLetterResolved = "platform.dead_letter_resolved";
    public const string InvalidPagination = "platform.invalid_pagination";
    public const string InvalidFilter = "platform.invalid_filter";
    public const string FinancialEventRequiresOperator = "platform.financial_event_requires_operator";

    // Idempotency / preconditions (PLT-205)
    public const string IdempotencyKeyRequired = "platform.idempotency_key_required";
    public const string IdempotencyKeyInvalid = "platform.idempotency_key_invalid";
    public const string IdempotencyKeyReuse = "platform.idempotency_key_reuse";
    public const string RequestInProgress = "platform.request_in_progress";
    public const string IdempotencyReplayTooLarge = "platform.idempotency_replay_too_large";
    public const string PreconditionRequired = "platform.precondition_required";
    public const string PreconditionFailed = "platform.precondition_failed";
    public const string RequestTooLarge = "platform.request_too_large";

    // Number series (PLT-206)
    public const string NumberSeriesNotFound = "platform.number_series_not_found";
    public const string SeriesLocked = "platform.series_locked";
    public const string SeriesStartConflict = "platform.series_start_conflict";
    public const string NumberProjectRequired = "platform.number_project_required";
    public const string NumberCollision = "platform.number_collision";
    public const string NumberBusy = "platform.number_busy";
    public const string NumberFormatInvalid = "platform.number_format_invalid";

    // Files (PLT-207)
    public const string FileNotFound = "platform.file_not_found";
    public const string FileTooLarge = "platform.file_too_large";
    public const string FileTypeNotAllowed = "platform.file_type_not_allowed";
    public const string FileSizeMismatch = "platform.file_size_mismatch";
    public const string FileQuarantined = "platform.file_quarantined";
    public const string FileNotAvailable = "platform.file_not_available";
    public const string FileAccessDenied = "platform.file_access_denied";
    public const string FileInUse = "platform.file_in_use";
    public const string FileLegalHold = "platform.file_legal_hold";
    public const string UploadExpired = "platform.upload_expired";
    public const string UploadIncomplete = "platform.upload_incomplete";
    public const string FileScanUnavailable = "platform.file_scan_unavailable";

    // Approvals (PLT-208)
    public const string ApprovalNoPolicy = "platform.approval_no_policy";
    public const string ApprovalPolicyOverlap = "platform.approval_policy_overlap";
    public const string ApprovalNotFound = "platform.approval_not_found";
    public const string ApprovalAlreadyDecided = "platform.approval_already_decided";
    public const string ApprovalSelfApprovalDenied = "platform.approval_self_approval_denied";
    public const string ApprovalNotApprover = "platform.approval_not_approver";
    public const string ApprovalReasonRequired = "platform.approval_reason_required";
    public const string ApprovalInvalidState = "platform.approval_invalid_state";

    // Notifications (PLT-209)
    public const string NotificationNotFound = "platform.notification_not_found";
    public const string NotificationPreferenceInvalid = "platform.notification_preference_invalid";
    public const string NotificationTemplateNotFound = "platform.notification_template_not_found";
    public const string NotificationTemplateInvalid = "platform.notification_template_invalid";
    public const string NotificationLinkInvalid = "platform.notification_link_invalid";
    public const string NotificationParamInvalid = "platform.notification_param_invalid";

    public static readonly IReadOnlyDictionary<string, (string En, string Ar)> Messages =
        new Dictionary<string, (string En, string Ar)>
        {
            [DeadMessageNotFound] = (
                "The requested dead-letter message was not found.",
                "رسالة صندوق الرسائل الميتة المطلوبة غير موجودة."),
            [DeadLetterResolved] = (
                "The dead-letter message has already been resolved.",
                "تمت معالجة رسالة صندوق الرسائل الميتة مسبقاً."),
            [InvalidPagination] = (
                "Invalid pagination parameters provided.",
                "معلمات الترقيم غير صحيحة."),
            [InvalidFilter] = (
                "Invalid filter parameters provided.",
                "معلمات التصفية غير صحيحة."),
            [FinancialEventRequiresOperator] = (
                "Financial dead letters can only be replayed or discarded by a platform operator.",
                "لا يمكن إعادة أو إسقاط الرسائل المالية الميتة إلا بواسطة مشغل المنصة."),

            [IdempotencyKeyRequired] = (
                "An Idempotency-Key header is required for this request.",
                "رأس Idempotency-Key مطلوب لهذا الطلب."),
            [IdempotencyKeyInvalid] = (
                "The Idempotency-Key header is invalid.",
                "رأس Idempotency-Key غير صالح."),
            [IdempotencyKeyReuse] = (
                "The Idempotency-Key was already used with a different request.",
                "تم استخدام Idempotency-Key مسبقاً مع طلب مختلف."),
            [RequestInProgress] = (
                "An identical request is still in progress; retry shortly.",
                "هناك طلب مطابق قيد التنفيذ؛ أعد المحاولة بعد قليل."),
            [IdempotencyReplayTooLarge] = (
                "The stored response is too large to replay; follow the Location header.",
                "الاستجابة المخزنة أكبر من أن يعاد تشغيلها؛ اتبع رأس Location."),
            [PreconditionRequired] = (
                "An If-Match header is required for this operation.",
                "رأس If-Match مطلوب لهذه العملية."),
            [PreconditionFailed] = (
                "The resource version does not match the supplied If-Match header.",
                "إصدار المورد لا يطابق رأس If-Match المقدم."),
            [RequestTooLarge] = (
                "The request body is too large.",
                "جسم الطلب أكبر من المسموح."),

            [NumberSeriesNotFound] = (
                "The number series was not found.",
                "سلسلة الترقيم غير موجودة."),
            [SeriesLocked] = (
                "The number series is locked and can no longer be changed.",
                "سلسلة الترقيم مقفلة ولم يعد من الممكن تغييرها."),
            [SeriesStartConflict] = (
                "The new start value conflicts with numbers already issued.",
                "قيمة البداية الجديدة تتعارض مع أرقام صادرة مسبقاً."),
            [NumberProjectRequired] = (
                "A project is required by this number series.",
                "هذه السلسلة تتطلب مشروعاً."),
            [NumberCollision] = (
                "A duplicate document number was detected.",
                "تم اكتشاف تكرار في رقم المستند."),
            [NumberBusy] = (
                "The number series is busy; retry the operation.",
                "سلسلة الترقيم مشغولة؛ أعد المحاولة."),
            [NumberFormatInvalid] = (
                "The number format is invalid.",
                "صيغة الترقيم غير صالحة."),

            [FileNotFound] = (
                "The file was not found.",
                "الملف غير موجود."),
            [FileTooLarge] = (
                "The file exceeds the allowed size.",
                "الملف يتجاوز الحجم المسموح."),
            [FileTypeNotAllowed] = (
                "The file type is not allowed.",
                "نوع الملف غير مسموح."),
            [FileSizeMismatch] = (
                "The uploaded size does not match the declared size.",
                "حجم الملف المرفوع لا يطابق الحجم المعلن."),
            [FileQuarantined] = (
                "The file is quarantined and cannot be used.",
                "الملف محجوز ولا يمكن استخدامه."),
            [FileNotAvailable] = (
                "The file is not available for download.",
                "الملف غير متاح للتنزيل."),
            [FileAccessDenied] = (
                "Access to this file is denied.",
                "الوصول إلى هذا الملف مرفوض."),
            [FileInUse] = (
                "The file is linked to a posted financial record and cannot be deleted.",
                "الملف مرتبط بمستند مالي مرحل ولا يمكن حذفه."),
            [FileLegalHold] = (
                "The file is under legal hold.",
                "الملف خاضع لحجز قانوني."),
            [UploadExpired] = (
                "The upload session has expired.",
                "انتهت صلاحية جلسة الرفع."),
            [UploadIncomplete] = (
                "The upload has not been received by storage.",
                "لم يتم استلام الملف في التخزين."),
            [FileScanUnavailable] = (
                "The content scanner is unavailable; the file remains quarantined.",
                "فاحص المحتوى غير متاح؛ يبقى الملف محجوزاً."),

            [ApprovalNoPolicy] = (
                "No approval policy matches this request.",
                "لا توجد سياسة موافقة مطابقة لهذا الطلب."),
            [ApprovalPolicyOverlap] = (
                "The approval policy overlaps another policy with the same specificity and priority.",
                "سياسة الموافقة تتعارض مع سياسة أخرى بنفس التخصيص والأولوية."),
            [ApprovalNotFound] = (
                "The approval instance was not found.",
                "طلب الموافقة غير موجود."),
            [ApprovalAlreadyDecided] = (
                "The approval step has already been decided.",
                "تم اتخاذ قرار بشأن خطوة الموافقة مسبقاً."),
            [ApprovalSelfApprovalDenied] = (
                "A requester cannot approve their own request.",
                "لا يمكن لمقدم الطلب الموافقة على طلبه."),
            [ApprovalNotApprover] = (
                "The current user is not an approver for this step.",
                "المستخدم الحالي ليس من المعتمدين لهذه الخطوة."),
            [ApprovalReasonRequired] = (
                "A rejection reason is required.",
                "سبب الرفض مطلوب."),
            [ApprovalInvalidState] = (
                "The approval is not in a state that allows this action.",
                "الموافقة ليست في حالة تسمح بهذا الإجراء."),

            [NotificationNotFound] = (
                "The notification was not found.",
                "الإشعار غير موجود."),
            [NotificationPreferenceInvalid] = (
                "The notification preference is invalid.",
                "تفضيلات الإشعارات غير صالحة."),
            [NotificationTemplateNotFound] = (
                "The notification template was not found.",
                "قالب الإشعار غير موجود."),
            [NotificationTemplateInvalid] = (
                "The notification template is invalid.",
                "قالب الإشعار غير صالح."),
            [NotificationLinkInvalid] = (
                "The notification link is not allowed.",
                "رابط الإشعار غير مسموح."),
            [NotificationParamInvalid] = (
                "The notification parameters are invalid.",
                "معلمات الإشعار غير صالحة.")
        };

    public static string English(string code) =>
        Messages.TryGetValue(code, out var text) ? text.En : code;

    public static string Arabic(string code) =>
        Messages.TryGetValue(code, out var text) ? text.Ar : code;
}
