using System.Globalization;

namespace ShoppingMall.Client.Admin;

// ============================================================================
// 차트 라이브러리를 쓰지 않고 직접 그리는 차트 컨트롤.
// Control 을 상속하고 Render() 에서 DrawingContext 로 선·사각형·글자·도형을 그린다.
// 데이터가 바뀌면 InvalidateVisual() 을 불러 "다시 그려 달라" 고 요청한다.
// 좌표는 컨트롤 왼쪽 위가 (0,0) 이고, 오른쪽으로 x, 아래로 y 가 커진다.
// ============================================================================

/// <summary>차트를 직접 그리는 컨트롤들이 함께 쓰는 색상·글자 도우미.</summary>
internal static class ChartStyle
{
    // 라이트/다크 테마 모두에서 읽히는 중간 회색
    public static readonly IBrush Text = new SolidColorBrush(Color.Parse("#8A8A8A"));
    public static readonly IPen Axis = new Pen(new SolidColorBrush(Color.Parse("#8A8A8A")), 1);
    public static readonly IPen Grid = new Pen(new SolidColorBrush(Color.FromArgb(50, 128, 128, 128)), 1);

    // 막대·도넛 조각 색 (파란 계열, 진한 색부터)
    public static readonly IBrush[] Palette =
    {
        new SolidColorBrush(Color.Parse("#0D47A1")),
        new SolidColorBrush(Color.Parse("#1976D2")),
        new SolidColorBrush(Color.Parse("#2196F3")),
        new SolidColorBrush(Color.Parse("#42A5F5")),
        new SolidColorBrush(Color.Parse("#64B5F6")),
        new SolidColorBrush(Color.Parse("#90CAF9")),
    };

    /// <summary>
    /// 글자 한 줄을 그린다. maxWidth 를 넘으면 뒤를 "…" 로 줄인다.
    /// align 은 maxWidth 폭 안에서의 정렬 (예: 축 눈금은 오른쪽 정렬, 막대 아래 이름은 가운데 정렬)
    /// </summary>
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

/// <summary>매출 TOP5 막대 차트 (단위: 만원). 세로축 눈금 + 막대 + 막대 위 금액 + 막대 아래 상품명.</summary>
public sealed class BarChart : Control
{
    private List<SalesEntry> _data = new();

    public BarChart()
    {
        MinHeight = 320;
        MinWidth = 300;
    }

    /// <summary>새 데이터를 넣고 다시 그린다.</summary>
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

        // 그림 영역의 여백: 왼쪽은 눈금 숫자, 위는 제목, 아래는 상품명이 들어갈 자리
        const double left = 52, right = 16, top = 40, bottom = 46;
        double plotW = Math.Max(10, width - left - right);
        double plotH = Math.Max(10, height - top - bottom);

        double maxValue = _data.Max(d => d.Sales) / 10000.0; // 원 → 만원

        // 가로 눈금선 + 세로축 눈금 값 (눈금 간격을 1·2·2.5·5·10 단위로 맞춰 반올림 없이 표시)
        const int ticks = 5;
        // 가장 큰 막대보다 10% 여유를 두고, 눈금 간격을 보기 좋은 값으로 맞춘다. (예: 최대 6 → 간격 2, 축 0~10)
        double step = NiceStep(maxValue * 1.1 / ticks);
        double axisMax = step * ticks;
        for (int i = 0; i <= ticks; i++)
        {
            double y = top + plotH - plotH * i / ticks; // 아래(0)에서 위로 올라가며
            ctx.DrawLine(ChartStyle.Grid, new Point(left, y), new Point(left + plotW, y));
            ChartStyle.DrawText(ctx, (step * i).ToString("#,0.##", CultureInfo.InvariantCulture),
                new Point(4, y - 8), 11, false, left - 8, TextAlignment.Right);
        }
        ctx.DrawLine(ChartStyle.Axis, new Point(left, top + plotH), new Point(left + plotW, top + plotH));

        // 가로 폭을 상품 수만큼 칸으로 나누고, 칸의 55% 를 막대 폭으로 쓴다. (나머지는 막대 사이 간격)
        double slot = plotW / _data.Count;
        double barW = slot * 0.55;
        for (int i = 0; i < _data.Count; i++)
        {
            double value = _data[i].Sales / 10000.0;
            double barH = plotH * value / axisMax; // 축 최대값 대비 비율만큼의 높이
            double x = left + slot * i + (slot - barW) / 2;
            double y = top + plotH - barH;

            ctx.DrawRectangle(ChartStyle.Palette[3], null, new Rect(x, y, barW, barH));
            ChartStyle.DrawText(ctx, value.ToString("N1", CultureInfo.InvariantCulture),
                new Point(x - 10, y - 18), 11, false, barW + 20, TextAlignment.Center);
            ChartStyle.DrawText(ctx, _data[i].Name,
                new Point(left + slot * i + 2, top + plotH + 6), 11, false, slot - 4, TextAlignment.Center);
        }
    }

    /// <summary>
    /// raw 이상인 가장 작은 1·2·2.5·5 × 10^n 값. 눈금이 1.32, 2.64 처럼 어색한 숫자가 되지 않게 한다.
    /// 예) raw=1.32 → 자릿수 1, 정규화 1.32 → 2 → 눈금 간격 2
    /// </summary>
    private static double NiceStep(double raw)
    {
        if (raw <= 0) return 0.2;
        double magnitude = Math.Pow(10, Math.Floor(Math.Log10(raw)));
        double normalized = raw / magnitude;
        double nice = normalized <= 1 ? 1 : normalized <= 2 ? 2 : normalized <= 2.5 ? 2.5 : normalized <= 5 ? 5 : 10;
        return nice * magnitude;
    }
}

/// <summary>카테고리별 매출 비중 도넛 차트. 매출 비율만큼의 각도로 고리 조각을 그리고, 오른쪽에 범례를 둔다.</summary>
public sealed class DonutChart : Control
{
    private List<SalesEntry> _data = new();

    public DonutChart()
    {
        MinHeight = 320;
        MinWidth = 300;
    }

    /// <summary>새 데이터를 넣고 다시 그린다. 매출 0 인 카테고리는 조각이 없으므로 뺀다.</summary>
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
            double sweep = 2 * Math.PI * _data[i].Sales / total; // 이 조각이 차지하는 각도 (라디안, 한 바퀴 = 2π)
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

    /// <summary>
    /// 중심 c, 바깥 반지름 outer, 안쪽 반지름 inner, 각도 a0→a1 인 고리 조각 모양.
    /// 바깥 호를 시계방향으로 그리고 → 안쪽으로 선을 긋고 → 안쪽 호를 반시계방향으로 되돌아와 닫는다.
    /// </summary>
    private static Geometry Ring(Point c, double outer, double inner, double a0, double a1)
    {
        // 중심에서 반지름 r, 각도 a 인 점의 좌표 (삼각함수)
        static Point At(Point c, double r, double a) => new(c.X + r * Math.Cos(a), c.Y + r * Math.Sin(a));

        bool large = a1 - a0 > Math.PI; // 반 바퀴보다 큰 호인지 (호를 그릴 때 알려 줘야 함)
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
