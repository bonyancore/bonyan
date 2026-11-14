namespace Construction.Platform.Contracts;

/// <summary>
/// Platform module error codes with bilingual (English and Arabic) messages.
/// </summary>
public static class PlatformErrorCodes
{
    public const string DeadMessageNotFound = "platform.dead_message_not_found";
    public const string DeadLetterResolved = "dead_letter_resolved";
    public const string InvalidPagination = "platform.invalid_pagination";
    public const string InvalidFilter = "platform.invalid_filter";

    public static readonly IReadOnlyDictionary<string, (string En, string Ar)> Messages =
        new Dictionary<string, (string En, string Ar)>
        {
            [DeadMessageNotFound] = (
                "The requested dead-letter message was not found.",
                "رسالة صندوق الرسائل الميتة المطلوبة غير موجودة."
            ),
            [DeadLetterResolved] = (
                "The dead-letter message has already been resolved.",
                "تمت معالجة رسالة صندوق الرسائل الميتة مسبقاً."
            ),
            [InvalidPagination] = (
                "Invalid pagination parameters provided.",
                "معلمات الترقيم غير صحيحة."
            ),
            [InvalidFilter] = (
                "Invalid filter parameters provided.",
                "معلمات التصفية غير صحيحة."
            )
        };
}
