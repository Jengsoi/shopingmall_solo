namespace ShoppingMall.Client.Views;

/// <summary>
/// 게시판 목록. 제목 검색, 페이지 이동(이전/다음), 글쓰기.
/// 목록에서 글을 더블클릭하면 상세 창(BoardDetailWindow)이 열린다.
/// </summary>
public sealed class BoardView : UserControl
{
    private const int PageSize = 20; // 한 페이지에 보여줄 글 수

    private readonly ShopApi _api;
    private readonly TextBox _keyword = Ui.Input("제목 검색...");
    private readonly DataGrid _table;
    private readonly Button _prev;
    private readonly Button _next;
    private readonly TextBlock _pageLabel = Ui.Text("1 페이지");
    private long _page = 1; // 지금 보고 있는 페이지 (1부터)

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

        // 이전/다음: 페이지 번호를 바꾸고 다시 조회
        _prev = Ui.Btn("이전", async () => { if (_page > 1) { _page--; await LoadPostsAsync(); } }, 70);
        _next = Ui.Btn("다음", async () => { _page++; await LoadPostsAsync(); }, 70);

        // 위쪽 한 줄: [제목] [검색어 입력(남은 폭)] [검색] [글쓰기] [새로고침]
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

    /// <summary>새로 검색하면 1페이지부터 보여준다.</summary>
    private Task SearchAsync()
    {
        _page = 1;
        return LoadPostsAsync();
    }

    /// <summary>현재 페이지·검색어로 글 목록을 불러오고, 페이지 표시와 이전/다음 버튼 상태를 맞춘다.</summary>
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

    /// <summary>글쓰기 창을 띄우고, 저장했으면 목록을 새로 고친다.</summary>
    private async Task WriteAsync()
    {
        if (TopLevel.GetTopLevel(this) is not Window owner) return;

        var dialog = new BoardWriteWindow(_api, post: null);
        if (await dialog.ShowDialog<bool>(owner))
            await LoadPostsAsync();
    }

    /// <summary>상세 창을 띄운다. 창 안에서 수정·삭제·댓글이 바뀌었을 수 있으니 닫히면 목록을 새로 고친다.</summary>
    private async Task OpenDetailAsync(long postId)
    {
        if (TopLevel.GetTopLevel(this) is not Window owner) return;

        var dialog = new BoardDetailWindow(_api, postId);
        await dialog.ShowDialog<bool>(owner);
        await LoadPostsAsync();
    }
}
