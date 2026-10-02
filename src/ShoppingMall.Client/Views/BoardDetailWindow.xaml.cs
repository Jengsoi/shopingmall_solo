namespace ShoppingMall.Client.Views;

/// <summary>
/// 게시글 상세보기 + 댓글. 글 수정/삭제, 댓글 등록/수정/삭제를 이 창에서 한다.
/// 수정·삭제 권한은 서버가 검사한다. (작성자가 아니면 서버가 거절하고 그 메시지를 보여준다)
/// </summary>
public partial class BoardDetailWindow : Window
{
    private readonly ShopApi _api;
    private readonly long _postId;
    private PostDetail? _post; // 지금 보여주는 글 (수정 창에 넘길 때 사용)

    public BoardDetailWindow(ShopApi api, long postId)
    {
        InitializeComponent();
        _api = api;
        _postId = postId;

        // 창이 화면에 뜬 뒤에 서버에서 글을 불러온다. (오류창을 띄우려면 창이 먼저 떠 있어야 한다)
        Loaded += (_, _) => Ui.Fire(this, LoadPostAsync);
    }

    private async void EditPost_Click(object sender, RoutedEventArgs e) => await Ui.Guard(this, EditPostAsync);
    private async void DeletePost_Click(object sender, RoutedEventArgs e) => await Ui.Guard(this, DeletePostAsync);
    private async void AddComment_Click(object sender, RoutedEventArgs e) => await Ui.Guard(this, AddCommentAsync);
    private async void EditComment_Click(object sender, RoutedEventArgs e) => await Ui.Guard(this, EditCommentAsync);
    private async void DeleteComment_Click(object sender, RoutedEventArgs e) => await Ui.Guard(this, DeleteCommentAsync);

    /// <summary>글과 댓글을 다시 불러온다. 글이 없어졌으면(다른 곳에서 삭제 등) 창을 닫는다.</summary>
    private async Task LoadPostAsync()
    {
        var result = await _api.BoardDetailAsync(_postId);
        if (!result.Ok)
        {
            Dialogs.Error(this, result.Message);
            Close();
            return;
        }

        _post = result.Value;
        TitleText.Text = _post.Title;
        MetaText.Text = _post.Meta;
        ContentBox.Text = _post.Content;
        CommentList.ItemsSource = _post.Comments;
    }

    /// <summary>글 수정 창을 띄우고, 저장했으면 내용을 새로 고친다.</summary>
    private async Task EditPostAsync()
    {
        if (_post is null) return;

        if (Dialogs.ShowDialog(new BoardWriteWindow(_api, _post), this))
            await LoadPostAsync();
    }

    /// <summary>확인 후 글 삭제. 성공하면 창을 닫는다.</summary>
    private async Task DeletePostAsync()
    {
        if (!Dialogs.Confirm(this, "게시글을 삭제하시겠습니까?", "삭제 확인"))
            return;

        var result = await _api.BoardDeleteAsync(_postId);
        if (result.Ok)
            Close();
        else
            Dialogs.Error(this, result.Message);
    }

    private async Task AddCommentAsync()
    {
        string content = (NewCommentBox.Text ?? "").Trim();
        if (content.Length == 0)
        {
            Dialogs.Info(this, "댓글 내용을 입력하세요.");
            return;
        }

        var result = await _api.CommentCreateAsync(_postId, content);
        if (result.Ok)
        {
            NewCommentBox.Text = "";
            await LoadPostAsync();
        }
        else
        {
            Dialogs.Error(this, result.Message);
        }
    }

    /// <summary>목록에서 고른 댓글을 입력 대화상자로 고친다.</summary>
    private async Task EditCommentAsync()
    {
        if (CommentList.SelectedItem is not CommentInfo comment)
        {
            Dialogs.Info(this, "수정할 댓글을 선택하세요.");
            return;
        }

        string? text = Dialogs.Prompt(this, "댓글 수정", "내용", comment.Content, multiLine: true);
        if (text is null) return; // 취소

        text = text.Trim();
        if (text.Length == 0)
        {
            Dialogs.Info(this, "댓글 내용을 입력하세요.");
            return;
        }

        var result = await _api.CommentUpdateAsync(comment.CommentId, text);
        if (result.Ok)
            await LoadPostAsync();
        else
            Dialogs.Error(this, result.Message);
    }

    private async Task DeleteCommentAsync()
    {
        if (CommentList.SelectedItem is not CommentInfo comment)
        {
            Dialogs.Info(this, "삭제할 댓글을 선택하세요.");
            return;
        }

        if (!Dialogs.Confirm(this, "댓글을 삭제하시겠습니까?", "삭제 확인"))
            return;

        var result = await _api.CommentDeleteAsync(comment.CommentId);
        if (result.Ok)
            await LoadPostAsync();
        else
            Dialogs.Error(this, result.Message);
    }
}
