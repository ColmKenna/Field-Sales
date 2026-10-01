namespace FieldSales.Identity.Pages.Shared;

public class ConfirmationModalModel
{
    public string DialogId { get; }
    public string Title { get; }
    public string Handler { get; }
    public string? ConfirmWord { get; }
    public string SubmitText { get; }
    public string SubmitClass { get; }
    public string? SubmitId { get; }
    public string? RouteName { get; }
    /// <summary>
    ///     Every hidden field the form posts, in render order: the single-field parameters
    ///     first, then any extras. List pages use the extras to carry pagination and filter
    ///     state through the round trip.
    /// </summary>
    public IReadOnlyList<ModalHiddenField> HiddenFields { get; }
    /// <summary>Literal markup. Use <c>{0}</c> placeholders for runtime values; see <see cref="BodyArgs" />.</summary>
    public string BodyHtml { get; }

    /// <summary>Runtime values substituted into <see cref="BodyHtml" />, HTML-encoded on render.</summary>
    public IReadOnlyList<string> BodyArgs { get; }
    public string? BodyId { get; }

    /// <summary>Optional <c>data-state</c> on the body paragraph.</summary>
    public string? BodyState { get; }

    /// <summary>
    ///     Literal markup for a second, initially hidden paragraph explaining why the action
    ///     is blocked. Script swaps which of the two paragraphs is visible.
    /// </summary>
    public string? BlockedBodyHtml { get; }

    public string? BlockedBodyId { get; }

    public ConfirmationModalModel(
        string dialogId,
        string title,
        string handler,
        string bodyHtml,
        string? confirmWord = null,
        string submitText = "Confirm",
        string submitClass = "btn-danger",
        string? submitId = null,
        string? routeName = null,
        string? hiddenInputName = null,
        string? hiddenInputId = null,
        string? hiddenInputValue = null,
        string? bodyId = null,
        IReadOnlyList<string>? bodyArgs = null,
        IReadOnlyList<ModalHiddenField>? hiddenFields = null,
        string? bodyState = null,
        string? blockedBodyHtml = null,
        string? blockedBodyId = null)
    {
        DialogId = dialogId;
        Title = title;
        Handler = handler;
        BodyHtml = bodyHtml;
        ConfirmWord = confirmWord;
        SubmitText = submitText;
        SubmitClass = submitClass;
        SubmitId = submitId;
        RouteName = routeName;
        BodyId = bodyId;
        BodyArgs = bodyArgs ?? Array.Empty<string>();
        BodyState = bodyState;
        BlockedBodyHtml = blockedBodyHtml;
        BlockedBodyId = blockedBodyId;

        List<ModalHiddenField> fields = [];
        if (!string.IsNullOrEmpty(hiddenInputName))
            fields.Add(new ModalHiddenField(hiddenInputName, hiddenInputId, hiddenInputValue));
        if (hiddenFields is not null)
            fields.AddRange(hiddenFields);
        HiddenFields = fields;
    }
}
