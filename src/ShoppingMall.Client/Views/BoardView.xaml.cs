namespace ShoppingMall.Client.Views;

/// <summary>
/// 게시판 목록. 제목 검색, 페이지 이동(이전/다음), 글쓰기.
/// 목록에서 글을 더블클릭하면 상세 창(BoardDetailWindow)이 열린다.
/// </summary>
public partial class BoardView : UserControl
{
    private const int PageSize = 20; // 한 페이지에 보여줄 글 수

    private readonly ShopApi _api;
    private long _page = 1; // 지금 보고 있는 페이지 (1부터)

    public BoardView(ShopApi api)
    {
        InitializeComponent();
        _api = api;
    }

    private async void Search_Click(object sender, RoutedEventArgs e) => await Ui.Guard(this, SearchAsync);
    private async void Refresh_Click(object sender, RoutedEventArgs e) => await Ui.Guard(this, LoadPostsAsync);
    private async void Write_Click(object sender, RoutedEventArgs e) => await Ui.Guard(this, WriteAsync);

    private void KeywordBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) Ui.Fire(this, SearchAsync);
    }

    // 이전/다음: 페이지 번호를 바꾸고 다시 조회
    private async void Prev_Click(object sender, RoutedEventArgs e)
    {
        if (_page <= 1) return;
        _page--;
        await Ui.Guard(this, LoadPostsAsync);
    }

    private async void Next_Click(object sender, RoutedEventArgs e)
    {
        _page++;
        await Ui.Guard(this, LoadPostsAsync);
    }

    private void Table_SelectionChanged(object sender, SelectionChangedEventArgs e) => e.Handled = true; // 바깥 TabControl 까지 올라가지 않게

    private void Table_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (Table.SelectedItem is PostSummary post)
            Ui.Fire(this, () => OpenDetailAsync(post.PostId));
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
        var result = await _api.BoardListAsync(_page, PageSize, (KeywordBox.Text ?? "").Trim());
        if (!result.Ok)
        {
            Dialogs.Error(this, result.Message);
            return;
        }

        var page = result.Value;
        Table.ItemsSource = page.Posts;
        PageText.Text = $"{_page} / {page.LastPage} 페이지 (전체 {page.Total}건)";
        PrevButton.IsEnabled = _page > 1;
        NextButton.IsEnabled = _page < page.LastPage;
    }

    /// <summary>글쓰기 창을 띄우고, 저장했으면 목록을 새로 고친다.</summary>
    private async Task WriteAsync()
    {
        if (Dialogs.ShowDialog(new BoardWriteWindow(_api, post: null), this))
            await LoadPostsAsync();
    }

    /// <summary>상세 창을 띄운다. 창 안에서 수정·삭제·댓글이 바뀌었을 수 있으니 닫히면 목록을 새로 고친다.</summary>
    private async Task OpenDetailAsync(long postId)
    {
        Dialogs.ShowDialog(new BoardDetailWindow(_api, postId), this);
        await LoadPostsAsync();
    }
}
