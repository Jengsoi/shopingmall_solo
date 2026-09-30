namespace ShoppingMall.Client.Views;

/// <summary>게시글 작성/수정 대화상자. post 가 주어지면 수정 모드 (gui/board_write_dialog.py)</summary>
public sealed class BoardWriteWindow : Window
{
    private readonly ShopApi _api;
    private readonly PostDetail? _post;
    private readonly TextBox _title = Ui.Input("제목");
    private readonly TextBox _content = Ui.MultiLine(260, "내용");

    public BoardWriteWindow(ShopApi api, PostDetail? post)
    {
        _api = api;
        _post = post;

        Title = post is null ? "게시글 작성" : "게시글 수정";
        Width = 520;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        if (post is not null)
        {
            _title.Text = post.Title;
            _content.Text = post.Content;
        }

        var cancel = Ui.Btn("취소", () => { Close(false); return Task.CompletedTask; });
        var buttons = Ui.HStack(8, Ui.Btn("저장", SaveAsync), cancel);
        buttons.HorizontalAlignment = HorizontalAlignment.Right;

        Content = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 8,
            Children = { Ui.Text("제목"), _title, Ui.Text("내용"), _content, buttons },
        };
    }

    private async Task SaveAsync()
    {
        string title = (_title.Text ?? "").Trim();
        string content = _content.Text ?? "";

        if (title.Length == 0)
        {
            await Dialogs.InfoAsync(this, "제목을 입력하세요.");
            return;
        }

        var result = _post is null
            ? await _api.BoardCreateAsync(title, content)
            : await _api.BoardUpdateAsync(_post.PostId, title, content);

        if (result.Ok)
            Close(true);
        else
            await Dialogs.ErrorAsync(this, result.Message);
    }
}
