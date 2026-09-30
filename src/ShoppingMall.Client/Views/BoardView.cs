namespace ShoppingMall.Client.Views;

/// <summary>게시판 목록 (gui/board_view.py)</summary>
public sealed class BoardView : UserControl
{
    private const int PageSize = 20;

    private readonly ShopApi _api;
    private readonly TextBox _keyword = Ui.Input("제목 검색...");
    private readonly DataGrid _table;
    private readonly Button _prev;
    private readonly Button _next;
    private readonly TextBlock _pageLabel = Ui.Text("1 페이지");
    private long _page = 1;

    public BoardView(ShopApi api)
    {
        _api = api;

        _table = Ui.Table(
            ("제목", nameof(PostSummary.Title), 4),
            ("작성자", nameof(PostSummary.Author), 1.2),
            ("작성일", nameof(PostSummary.CreatedAt), 2),
            ("댓글", nameof(PostSummary.CommentCount), 0.7));
        _table.DoubleTapped += (_, _) =>
        {
            if (_table.SelectedItem is PostSummary post)
                Ui.Fire(this, () => OpenDetailAsync(post.PostId));
        };

        _keyword.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) Ui.Fire(this, SearchAsync);
        };

        _prev = Ui.Btn("이전", async () => { if (_page > 1) { _page--; await LoadPostsAsync(); } }, 70);
        _next = Ui.Btn("다음", async () => { _page++; await LoadPostsAsync(); }, 70);

        var topRow = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto,Auto"), ColumnSpacing = 8 };
        var title = Ui.Title("[게시판]");
        var search = Ui.Btn("검색", SearchAsync);
        var write = Ui.Btn("글쓰기", WriteAsync);
        var refresh = Ui.Btn("새로고침", LoadPostsAsync);
        Grid.SetColumn(title, 0);
        Grid.SetColumn(_keyword, 1);
        Grid.SetColumn(search, 2);
        Grid.SetColumn(write, 3);
        Grid.SetColumn(refresh, 4);
        topRow.Children.Add(title);
        topRow.Children.Add(_keyword);
        topRow.Children.Add(search);
        topRow.Children.Add(write);
        topRow.Children.Add(refresh);
        topRow.Margin = new Thickness(0, 0, 0, 8);

        var pager = Ui.HStack(12, _prev, _pageLabel, _next);
        pager.HorizontalAlignment = HorizontalAlignment.Center;
        pager.Margin = new Thickness(0, 8, 0, 0);

        Content = new Border { Padding = new Thickness(12), Child = Ui.Dock(topRow, _table, pager) };
    }

    private Task SearchAsync()
    {
        _page = 1;
        return LoadPostsAsync();
    }

    public async Task LoadPostsAsync()
    {
        var result = await _api.BoardListAsync(_page, PageSize, (_keyword.Text ?? "").Trim());
        if (!result.Ok)
        {
            await Dialogs.ErrorAsync(this, result.Message);
            return;
        }

        var page = result.Value;
        _table.ItemsSource = page.Posts;
        _pageLabel.Text = $"{_page} / {page.LastPage} 페이지 (전체 {page.Total}건)";
        _prev.IsEnabled = _page > 1;
        _next.IsEnabled = _page < page.LastPage;
    }

    private async Task WriteAsync()
    {
        if (TopLevel.GetTopLevel(this) is not Window owner) return;

        var dialog = new BoardWriteWindow(_api, post: null);
        if (await dialog.ShowDialog<bool>(owner))
            await LoadPostsAsync();
    }

    private async Task OpenDetailAsync(long postId)
    {
        if (TopLevel.GetTopLevel(this) is not Window owner) return;

        var dialog = new BoardDetailWindow(_api, postId);
        await dialog.ShowDialog<bool>(owner);
        await LoadPostsAsync();
    }
}
