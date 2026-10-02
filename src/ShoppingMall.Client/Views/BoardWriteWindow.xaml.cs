namespace ShoppingMall.Client.Views;

/// <summary>
/// 게시글 작성/수정 대화상자. post 가 null 이면 새 글, 주어지면 그 글을 수정하는 모드.
/// 저장에 성공하면 DialogResult = true 로 닫아 부른 쪽이 목록을 새로 고치게 한다.
/// </summary>
public partial class BoardWriteWindow : Window
{
    private readonly ShopApi _api;
    private readonly PostDetail? _post;

    public BoardWriteWindow(ShopApi api, PostDetail? post)
    {
        InitializeComponent();
        _api = api;
        _post = post;

        Title = post is null ? "게시글 작성" : "게시글 수정";

        // 수정 모드: 기존 제목·내용을 채워 둔다.
        if (post is not null)
        {
            TitleBox.Text = post.Title;
            ContentBox.Text = post.Content;
        }

        Loaded += (_, _) => TitleBox.Focus();
    }

    private async void Save_Click(object sender, RoutedEventArgs e) => await Ui.Guard(this, SaveAsync);

    /// <summary>새 글이면 작성, 기존 글이면 수정 요청을 보낸다.</summary>
    private async Task SaveAsync()
    {
        string title = (TitleBox.Text ?? "").Trim();
        string content = ContentBox.Text ?? "";

        if (title.Length == 0)
        {
            Dialogs.Info(this, "제목을 입력하세요.");
            return;
        }

        var result = _post is null
            ? await _api.BoardCreateAsync(title, content)
            : await _api.BoardUpdateAsync(_post.PostId, title, content);

        if (result.Ok)
            DialogResult = true;
        else
            Dialogs.Error(this, result.Message);
    }
}
