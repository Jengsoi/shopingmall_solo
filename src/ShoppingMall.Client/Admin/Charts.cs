using System.Globalization;

namespace ShoppingMall.Client.Admin;

/// <summary>차트를 직접 그리는 컨트롤들이 함께 쓰는 색상·글자 도우미.</summary>
internal static class ChartStyle
{
    // 라이트/다크 테마 모두에서 읽히는 중간 회색
    public static readonly IBrush Text = new SolidColorBrush(Color.Parse("#8A8A8A"));
    public static readonly IPen Axis = new Pen(new SolidColorBrush(Color.Parse("#8A8A8A")), 1);
    public static readonly IPen Grid = new Pen(new SolidColorBrush(Color.FromArgb(50, 128, 128, 128)), 1);

    // 원본(dashboard_view.py)의 파란 계열 팔레트
    public static readonly IBrush[] Palette =
    {
        new SolidColorBrush(Color.Parse("#0D47A1")),
        new SolidColorBrush(Color.Parse("#1976D2")),
        new SolidColorBrush(Color.Parse("#2196F3")),
        new SolidColorBrush(Color.Parse("#42A5F5")),
        new SolidColorBrush(Color.Parse("#64B5F6")),
        new SolidColorBrush(Color.Parse("#90CAF9")),
    };

    public static void DrawText(DrawingContext ctx, string text, Point origin, double size, bool bold = false,
        double maxWidth = double.PositiveInfinity, TextAlignment align = TextAlignment.Left)
    {
        var formatted = new FormattedText(
            text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            new Typeface(FontFamily.Default, FontStyle.Normal, bold ? FontWeight.Bold : FontWeight.Normal),
            size, Text)
        {
            MaxTextWidth = maxWidth,
            MaxLineCount = 1,
            Trimming = TextTrimming.CharacterEllipsis,
            TextAlignment = align,
        };
        ctx.DrawText(formatted, origin);
    }
}

/// <summary>매출 TOP5 막대 차트 (단위: 만원)</summary>
public sealed class BarChart : Control
{
    private List<SalesEntry> _data = new();

    public BarChart()
    {
        MinHeight = 320;
        MinWidth = 300;
    }

    public void SetData(IEnumerable<SalesEntry> data)
    {
        _data = data.ToList();
        InvalidateVisual();
    }

    public override void Render(DrawingContext ctx)
    {
        base.Render(ctx);

        double width = Bounds.Width, height = Bounds.Height;
        ChartStyle.DrawText(ctx, "매출 TOP 5 (매출액 기준 / 단위: 만원)", new Point(8, 6), 14, true, width - 16);

        if (_data.Count == 0)
        {
            ChartStyle.DrawText(ctx, "데이터가 없습니다.", new Point(8, height / 2), 13);
            return;
        }

        const double left = 52, right = 16, top = 40, bottom = 46;
        double plotW = Math.Max(10, width - left - right);
        double plotH = Math.Max(10, height - top - bottom);

        double maxValue = _data.Max(d => d.Sales) / 10000.0;

        // 가로 눈금선 + 세로축 눈금 값 (눈금 간격을 1·2·2.5·5·10 단위로 맞춰 반올림 없이 표시)
        const int ticks = 5;
        double step = NiceStep(maxValue * 1.1 / ticks);
        double axisMax = step * ticks;
        for (int i = 0; i <= ticks; i++)
        {
            double y = top + plotH - plotH * i / ticks;
            ctx.DrawLine(ChartStyle.Grid, new Point(left, y), new Point(left + plotW, y));
            ChartStyle.DrawText(ctx, (step * i).ToString("#,0.##", CultureInfo.InvariantCulture),
                new Point(4, y - 8), 11, false, left - 8, TextAlignment.Right);
        }
        ctx.DrawLine(ChartStyle.Axis, new Point(left, top + plotH), new Point(left + plotW, top + plotH));

        double slot = plotW / _data.Count;
        double barW = slot * 0.55;
        for (int i = 0; i < _data.Count; i++)
        {
            double value = _data[i].Sales / 10000.0;
            double barH = plotH * value / axisMax;
            double x = left + slot * i + (slot - barW) / 2;
            double y = top + plotH - barH;

            ctx.DrawRectangle(ChartStyle.Palette[3], null, new Rect(x, y, barW, barH));
            ChartStyle.DrawText(ctx, value.ToString("N1", CultureInfo.InvariantCulture),
                new Point(x - 10, y - 18), 11, false, barW + 20, TextAlignment.Center);
            ChartStyle.DrawText(ctx, _data[i].Name,
                new Point(left + slot * i + 2, top + plotH + 6), 11, false, slot - 4, TextAlignment.Center);
        }
    }

    /// <summary>raw 이상인 가장 작은 1·2·2.5·5 × 10^n 값</summary>
    private static double NiceStep(double raw)
    {
        if (raw <= 0) return 0.2;
        double magnitude = Math.Pow(10, Math.Floor(Math.Log10(raw)));
        double normalized = raw / magnitude;
        double nice = normalized <= 1 ? 1 : normalized <= 2 ? 2 : normalized <= 2.5 ? 2.5 : normalized <= 5 ? 5 : 10;
        return nice * magnitude;
    }
}

/// <summary>카테고리별 매출 비중 도넛 차트</summary>
public sealed class DonutChart : Control
{
    private List<SalesEntry> _data = new();

    public DonutChart()
    {
        MinHeight = 320;
        MinWidth = 300;
    }

    public void SetData(IEnumerable<SalesEntry> data)
    {
        _data = data.Where(d => d.Sales > 0).ToList();
        InvalidateVisual();
    }

    public override void Render(DrawingContext ctx)
    {
        base.Render(ctx);

        double width = Bounds.Width, height = Bounds.Height;
        ChartStyle.DrawText(ctx, "카테고리별 매출 비중", new Point(8, 6), 14, true, width - 16);

        long total = _data.Sum(d => d.Sales);
        if (total <= 0)
        {
            ChartStyle.DrawText(ctx, "데이터가 없습니다.", new Point(8, height / 2), 13);
            return;
        }

        // 왼쪽에 도넛, 오른쪽에 범례
        double legendW = Math.Min(180, width * 0.4);
        double areaW = width - legendW - 16;
        double outer = Math.Max(20, Math.Min(areaW, height - 60) / 2);
        double inner = outer * 0.5;
        var center = new Point(8 + areaW / 2, 40 + (height - 40) / 2);

        double angle = -Math.PI / 2; // 12시 방향에서 시작
        for (int i = 0; i < _data.Count; i++)
        {
            double sweep = 2 * Math.PI * _data[i].Sales / total;
            sweep = Math.Min(sweep, 2 * Math.PI - 0.0001); // 원 전체 한 조각일 때 호가 사라지는 것을 방지

            var brush = ChartStyle.Palette[i % ChartStyle.Palette.Length];
            ctx.DrawGeometry(brush, null, Ring(center, outer, inner, angle, angle + sweep));
            angle += sweep;

            // 범례
            double ly = 50 + i * 24;
            double lx = 8 + areaW + 12;
            ctx.DrawRectangle(brush, null, new Rect(lx, ly + 2, 12, 12));
            double percent = _data[i].Sales * 100.0 / total;
            ChartStyle.DrawText(ctx, $"{_data[i].Name} {percent.ToString("0.0", CultureInfo.InvariantCulture)}%",
                new Point(lx + 18, ly), 12, false, legendW - 30);
        }
    }

    private static Geometry Ring(Point c, double outer, double inner, double a0, double a1)
    {
        static Point At(Point c, double r, double a) => new(c.X + r * Math.Cos(a), c.Y + r * Math.Sin(a));

        bool large = a1 - a0 > Math.PI;
        var geometry = new StreamGeometry();
        using (var s = geometry.Open())
        {
            s.BeginFigure(At(c, outer, a0), true);
            s.ArcTo(At(c, outer, a1), new Size(outer, outer), 0, large, SweepDirection.Clockwise);
            s.LineTo(At(c, inner, a1));
            s.ArcTo(At(c, inner, a0), new Size(inner, inner), 0, large, SweepDirection.CounterClockwise);
            s.EndFigure(true);
        }
        return geometry;
    }
}
