namespace ShoppingMall.Client.Views;

/// <summary>게시글 상세보기 + 댓글 (gui/board_detail_dialog.py)</summary>
public sealed class BoardDetailWindow : Window
{
    private readonly ShopApi _api;
    private readonly long _postId;
    private readonly TextBlock _title = Ui.Text("", 16, true);
    private readonly TextBlock _meta = Ui.Text("");
    private readonly TextBox _content = new() { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Height = 160 };
    private readonly ListBox _comments = new() { Height = 140 };
    private readonly TextBox _newComment = Ui.MultiLine(60, "댓글을 입력하세요...");
    private PostDetail? _post;

    public BoardDetailWindow(ShopApi api, long postId)
    {
        _api = api;
        _postId = postId;

        Title = "게시글 보기";
        Width = 560;
        Height = 700;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var postButtons = Ui.HStack(8, Ui.Btn("수정", EditPostAsync), Ui.Btn("삭제", DeletePostAsync));
        postButtons.HorizontalAlignment = HorizontalAlignment.Right;

        var commentButtons = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
        var add = Ui.Btn("댓글 등록", AddCommentAsync);
        var editDelete = Ui.HStack(8, Ui.Btn("댓글 수정", EditCommentAsync), Ui.Btn("댓글 삭제", DeleteCommentAsync));
        Grid.SetColumn(add, 0);
        Grid.SetColumn(editDelete, 2);
        commentButtons.Children.Add(add);
        commentButtons.Children.Add(editDelete);

        var close = Ui.Btn("닫기", () => { Close(true); return Task.CompletedTask; });
        close.HorizontalAlignment = HorizontalAlignment.Right;

        Content = new ScrollViewer
        {
            Content = new StackPanel
            {
                Margin = new Thickness(20),
                Spacing = 8,
                Children =
                {
                    _title, _meta, _content, postButtons,
                    Ui.Text("댓글", 14, true), _comments, _newComment, commentButtons, close,
                },
            },
        };

        Opened += (_, _) => Ui.Fire(this, LoadPostAsync);
    }

    private async Task LoadPostAsync()
    {
        var result = await _api.BoardDetailAsync(_postId);
        if (!result.Ok)
        {
            await Dialogs.ErrorAsync(this, result.Message);
            Close(false);
            return;
        }

        _post = result.Value;
        _title.Text = _post.Title;
        _meta.Text = _post.Meta;
        _content.Text = _post.Content;
        _comments.ItemsSource = _post.Comments;
    }

    private async Task EditPostAsync()
    {
        if (_post is null) return;

        var dialog = new BoardWriteWindow(_api, _post);
        if (await dialog.ShowDialog<bool>(this))
            await LoadPostAsync();
    }

    private async Task DeletePostAsync()
    {
        if (!await Dialogs.ConfirmAsync(this, "게시글을 삭제하시겠습니까?", "삭제 확인"))
            return;

        var result = await _api.BoardDeleteAsync(_postId);
        if (result.Ok)
            Close(true);
        else
            await Dialogs.ErrorAsync(this, result.Message);
    }

    private async Task AddCommentAsync()
    {
        string content = (_newComment.Text ?? "").Trim();
        if (content.Length == 0)
        {
            await Dialogs.InfoAsync(this, "댓글 내용을 입력하세요.");
            return;
        }

        var result = await _api.CommentCreateAsync(_postId, content);
        if (result.Ok)
        {
            _newComment.Text = "";
            await LoadPostAsync();
        }
        else
        {
            await Dialogs.ErrorAsync(this, result.Message);
        }
    }

    private async Task EditCommentAsync()
    {
        if (_comments.SelectedItem is not CommentInfo comment)
        {
            await Dialogs.InfoAsync(this, "수정할 댓글을 선택하세요.");
            return;
        }

        string? text = await Dialogs.PromptAsync(this, "댓글 수정", "내용", comment.Content, multiLine: true);
        if (text is null) return;

        text = text.Trim();
        if (text.Length == 0)
        {
            await Dialogs.InfoAsync(this, "댓글 내용을 입력하세요.");
            return;
        }

        var result = await _api.CommentUpdateAsync(comment.CommentId, text);
        if (result.Ok)
            await LoadPostAsync();
        else
            await Dialogs.ErrorAsync(this, result.Message);
    }

    private async Task DeleteCommentAsync()
    {
        if (_comments.SelectedItem is not CommentInfo comment)
        {
            await Dialogs.InfoAsync(this, "삭제할 댓글을 선택하세요.");
            return;
        }

        if (!await Dialogs.ConfirmAsync(this, "댓글을 삭제하시겠습니까?", "삭제 확인"))
            return;

        var result = await _api.CommentDeleteAsync(comment.CommentId);
        if (result.Ok)
            await LoadPostAsync();
        else
            await Dialogs.ErrorAsync(this, result.Message);
    }
}
