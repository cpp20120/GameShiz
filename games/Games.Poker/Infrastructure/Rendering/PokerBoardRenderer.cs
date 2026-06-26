using System.Globalization;
using SkiaSharp;

namespace Games.Poker.Infrastructure.Rendering;

public static class PokerBoardRenderer
{
    private const int Width = 1100;
    private const int Height = 720;
    private const int CardWidth = 96;
    private const int CardHeight = 136;

    private static readonly SKColor Felt = SKColor.Parse("#0f5132");
    private static readonly SKColor FeltDark = SKColor.Parse("#0b3d2a");
    private static readonly SKColor Rail = SKColor.Parse("#5b341b");
    private static readonly SKColor Gold = SKColor.Parse("#f5c542");
    private static readonly SKColor Text = SKColors.White;
    private static readonly SKColor MutedText = SKColor.Parse("#cbd5e1");

    public static byte[] Render(TableSnapshot snapshot, ILocalizer localizer)
    {
        using var surface = SKSurface.Create(new SKImageInfo(Width, Height));
        var canvas = surface.Canvas;
        canvas.Clear(SKColor.Parse("#09251c"));

        using var typeface = SKTypeface.FromFamilyName("DejaVu Sans");
        using var monoTypeface = SKTypeface.FromFamilyName("monospace");
        using var titleFont = new SKFont(typeface, 38);
        titleFont.Embolden = true;
        using var bodyFont = new SKFont(typeface, 28);
        using var smallFont = new SKFont(typeface, 22);
        using var cardRankFont = new SKFont(monoTypeface, 34);
        cardRankFont.Embolden = true;

        DrawTable(canvas);
        DrawHeader(canvas, snapshot.Table, localizer, titleFont, bodyFont);
        DrawCommunity(canvas, snapshot.Table, cardRankFont, bodyFont);
        DrawPlayers(canvas, snapshot.Table, snapshot.Seats, bodyFont, smallFont);

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 92);
        return data.ToArray();
    }

    public static string Caption(PokerTable table, ILocalizer localizer)
    {
        var phase = PhaseName(table.Phase, localizer);
        return $"🃏 <b>{string.Format(localizer.Get("poker", "state.header"), table.InviteCode)}</b> · {phase}\n"
            + string.Create(CultureInfo.InvariantCulture, $"Банк: <b>{table.Pot}</b> · Ставка: <b>{table.CurrentBet}</b>");
    }

    private static void DrawTable(SKCanvas canvas)
    {
        using var railPaint = new SKPaint();
        railPaint.Color = Rail;
        railPaint.IsAntialias = true;
        using var feltPaint = new SKPaint();
        feltPaint.Color = Felt;
        feltPaint.IsAntialias = true;
        using var innerPaint = new SKPaint();
        innerPaint.Color = FeltDark;
        innerPaint.IsAntialias = true;
        innerPaint.Style = SKPaintStyle.Stroke;
        innerPaint.StrokeWidth = 5;

        canvas.DrawRoundRect(new SKRoundRect(new SKRect(70, 120, Width - 70, Height - 50), 230, 230), railPaint);
        canvas.DrawRoundRect(new SKRoundRect(new SKRect(105, 155, Width - 105, Height - 85), 190, 190), feltPaint);
        canvas.DrawRoundRect(new SKRoundRect(new SKRect(145, 195, Width - 145, Height - 125), 150, 150), innerPaint);
    }

    private static void DrawHeader(SKCanvas canvas, PokerTable table, ILocalizer localizer, SKFont titleFont, SKFont bodyFont)
    {
        using var titlePaint = new SKPaint();
        titlePaint.Color = Text;
        titlePaint.IsAntialias = true;
        using var goldPaint = new SKPaint();
        goldPaint.Color = Gold;
        goldPaint.IsAntialias = true;
        using var panelPaint = new SKPaint();
        panelPaint.Color = new SKColor(0, 0, 0, 120);
        panelPaint.IsAntialias = true;

        canvas.DrawRoundRect(new SKRoundRect(new SKRect(40, 30, Width - 40, 100), 22, 22), panelPaint);
        canvas.DrawText($"Poker {table.InviteCode}", 70, 77, SKTextAlign.Left, titleFont, titlePaint);
        canvas.DrawText(PhaseName(table.Phase, localizer), Width - 70, 75, SKTextAlign.Right, bodyFont, goldPaint);
    }

    private static void DrawCommunity(SKCanvas canvas, PokerTable table, SKFont rankFont, SKFont bodyFont)
    {
        var community = table.Phase switch
        {
            PokerPhase.PreFlop or PokerPhase.None => Array.Empty<string>(),
            PokerPhase.Flop => Deck.Parse(table.CommunityCards).Take(3).ToArray(),
            PokerPhase.Turn => Deck.Parse(table.CommunityCards).Take(4).ToArray(),
            _ => Deck.Parse(table.CommunityCards).Take(5).ToArray(),
        };

        const int totalWidth = (5 * CardWidth) + (4 * 18);
        var startX = (Width - totalWidth) / 2f;
        const float y = 292;

        using var labelPaint = new SKPaint();
        labelPaint.Color = MutedText;
        labelPaint.IsAntialias = true;
        canvas.DrawText("Community", Width / 2f, y - 28, SKTextAlign.Center, bodyFont, labelPaint);

        for (var i = 0; i < 5; i++)
        {
            var x = startX + (i * (CardWidth + 18));
            if (i < community.Length) DrawCard(canvas, community[i], x, y, rankFont);
            else DrawCardBack(canvas, x, y);
        }
    }

    private static void DrawPlayers(SKCanvas canvas, PokerTable table, IList<PokerSeat> seats, SKFont bodyFont, SKFont smallFont)
    {
        var ordered = seats.OrderBy(s => s.Position).ToList();
        if (ordered.Count == 0) return;

        const float centerX = Width / 2f;
        const float centerY = 386f;
        const float radiusX = 420f;
        const float radiusY = 245f;

        for (var i = 0; i < ordered.Count; i++)
        {
            var seat = ordered[i];
            var angle = (-Math.PI / 2) + (i * (2 * Math.PI / ordered.Count));
            var x = centerX + ((float)Math.Cos(angle) * radiusX);
            var y = centerY + ((float)Math.Sin(angle) * radiusY);
            DrawPlayer(canvas, table, seat, x, y, bodyFont, smallFont);
        }
    }

    private static void DrawPlayer(SKCanvas canvas, PokerTable table, PokerSeat seat, float x, float y, SKFont bodyFont, SKFont smallFont)
    {
        var isCurrent = table.Status == PokerTableStatus.HandActive && seat.Position == table.CurrentSeat;
        var isButton = seat.Position == table.ButtonSeat;
        var panelColor = seat.Status switch
        {
            PokerSeatStatus.Folded => new SKColor(40, 40, 40, 210),
            PokerSeatStatus.AllIn => new SKColor(87, 58, 8, 230),
            PokerSeatStatus.SittingOut => new SKColor(31, 41, 55, 210),
            _ when isCurrent => new SKColor(20, 83, 45, 245),
            _ => new SKColor(15, 23, 42, 225),
        };

        using var panelPaint = new SKPaint();
        panelPaint.Color = panelColor;
        panelPaint.IsAntialias = true;
        using var borderPaint = new SKPaint();
        borderPaint.Color = isCurrent ? Gold : new SKColor(148, 163, 184, 120);
        borderPaint.IsAntialias = true;
        borderPaint.Style = SKPaintStyle.Stroke;
        borderPaint.StrokeWidth = isCurrent ? 5 : 2;
        using var textPaint = new SKPaint();
        textPaint.Color = Text;
        textPaint.IsAntialias = true;
        using var mutedPaint = new SKPaint();
        mutedPaint.Color = MutedText;
        mutedPaint.IsAntialias = true;
        using var goldPaint = new SKPaint();
        goldPaint.Color = Gold;
        goldPaint.IsAntialias = true;

        var rect = new SKRect(x - 120, y - 48, x + 120, y + 58);
        canvas.DrawRoundRect(new SKRoundRect(rect, 18, 18), panelPaint);
        canvas.DrawRoundRect(new SKRoundRect(rect, 18, 18), borderPaint);

        var name = Truncate(seat.DisplayName, 16);
        canvas.DrawText(name, x, y - 15, SKTextAlign.Center, bodyFont, textPaint);

        var status = seat.Status switch
        {
            PokerSeatStatus.Folded => "fold",
            PokerSeatStatus.AllIn => "all-in",
            PokerSeatStatus.SittingOut => "out",
            _ => seat.CurrentBet > 0 ? $"bet {seat.CurrentBet}" : "ready",
        };
        canvas.DrawText($"{seat.Stack} · {status}", x, y + 24, SKTextAlign.Center, smallFont, mutedPaint);

        if (isButton)
        {
            canvas.DrawCircle(rect.Left + 22, rect.Top + 20, 16, goldPaint);
            using var btnTextPaint = new SKPaint();
            btnTextPaint.Color = SKColor.Parse("#111827");
            btnTextPaint.IsAntialias = true;
            canvas.DrawText("D", rect.Left + 22, rect.Top + 28, SKTextAlign.Center, smallFont, btnTextPaint);
        }

        if (isCurrent)
            canvas.DrawText("TURN", rect.Right - 14, rect.Top + 27, SKTextAlign.Right, smallFont, goldPaint);
    }

    private static void DrawCard(SKCanvas canvas, string card, float x, float y, SKFont rankFont)
    {
        var rank = card.Length > 0 && card[0] == 'T' ? "10" : card.Length > 0 ? card[0].ToString() : "?";
        var suit = card.Length > 1 ? card[1] : '?';
        var suitColor = suit is 'H' or 'D' ? SKColor.Parse("#dc2626") : SKColor.Parse("#111827");

        using var cardPaint = new SKPaint();
        cardPaint.Color = SKColors.White;
        cardPaint.IsAntialias = true;
        using var borderPaint = new SKPaint();
        borderPaint.Color = new SKColor(15, 23, 42, 180);
        borderPaint.IsAntialias = true;
        borderPaint.Style = SKPaintStyle.Stroke;
        borderPaint.StrokeWidth = 3;
        using var textPaint = new SKPaint();
        textPaint.Color = suitColor;
        textPaint.IsAntialias = true;

        var rect = new SKRect(x, y, x + CardWidth, y + CardHeight);
        canvas.DrawRoundRect(new SKRoundRect(rect, 12, 12), cardPaint);
        canvas.DrawRoundRect(new SKRoundRect(rect, 12, 12), borderPaint);
        canvas.DrawText(rank, x + 16, y + 38, SKTextAlign.Left, rankFont, textPaint);
        canvas.DrawText(rank, x + CardWidth - 16, y + CardHeight - 16, SKTextAlign.Right, rankFont, textPaint);
        DrawSuitIcon(canvas, suit, x + (CardWidth / 2f), y + (CardHeight / 2f) + 4, 18, suitColor);
    }

    private static void DrawSuitIcon(SKCanvas canvas, char suit, float cx, float cy, float size, SKColor color)
    {
        using var paint = new SKPaint();
        paint.Color = color;
        paint.IsAntialias = true;
        paint.Style = SKPaintStyle.Fill;
        switch (suit)
        {
            case 'H':
                canvas.DrawCircle(cx - (size * 0.45f), cy - (size * 0.25f), size * 0.45f, paint);
                canvas.DrawCircle(cx + (size * 0.45f), cy - (size * 0.25f), size * 0.45f, paint);
                using (var path = new SKPath())
                {
                    path.MoveTo(cx - (size * 0.9f), cy - (size * 0.1f));
                    path.LineTo(cx + (size * 0.9f), cy - (size * 0.1f));
                    path.LineTo(cx, cy + (size * 1.05f));
                    path.Close();
                    canvas.DrawPath(path, paint);
                }
                break;
            case 'D':
                using (var path = new SKPath())
                {
                    path.MoveTo(cx, cy - (size * 1.05f));
                    path.LineTo(cx + (size * 0.8f), cy);
                    path.LineTo(cx, cy + (size * 1.05f));
                    path.LineTo(cx - (size * 0.8f), cy);
                    path.Close();
                    canvas.DrawPath(path, paint);
                }
                break;
            case 'C':
                canvas.DrawCircle(cx, cy - (size * 0.55f), size * 0.42f, paint);
                canvas.DrawCircle(cx - (size * 0.5f), cy + (size * 0.08f), size * 0.42f, paint);
                canvas.DrawCircle(cx + (size * 0.5f), cy + (size * 0.08f), size * 0.42f, paint);
                canvas.DrawRect(new SKRect(
                    cx - (size * 0.14f),
                    cy + (size * 0.1f),
                    cx + (size * 0.14f),
                    cy + (size * 0.95f)), paint);
                break;
            case 'S':
                canvas.DrawCircle(cx - (size * 0.42f), cy + (size * 0.18f), size * 0.42f, paint);
                canvas.DrawCircle(cx + (size * 0.42f), cy + (size * 0.18f), size * 0.42f, paint);
                using (var path = new SKPath())
                {
                    path.MoveTo(cx - (size * 0.85f), cy + (size * 0.02f));
                    path.LineTo(cx + (size * 0.85f), cy + (size * 0.02f));
                    path.LineTo(cx, cy - (size * 1.05f));
                    path.Close();
                    canvas.DrawPath(path, paint);
                }
                canvas.DrawRect(new SKRect(
                    cx - (size * 0.14f),
                    cy + (size * 0.15f),
                    cx + (size * 0.14f),
                    cy + (size * 0.95f)), paint);
                break;
            default:
                canvas.DrawCircle(cx, cy, size * 0.7f, paint);
                break;
        }
    }

    private static void DrawCardBack(SKCanvas canvas, float x, float y)
    {
        using var backPaint = new SKPaint();
        backPaint.Color = SKColor.Parse("#1d4ed8");
        backPaint.IsAntialias = true;
        using var borderPaint = new SKPaint();
        borderPaint.Color = SKColors.White;
        borderPaint.IsAntialias = true;
        borderPaint.Style = SKPaintStyle.Stroke;
        borderPaint.StrokeWidth = 3;
        using var patternPaint = new SKPaint();
        patternPaint.Color = new SKColor(255, 255, 255, 80);
        patternPaint.IsAntialias = true;
        patternPaint.Style = SKPaintStyle.Stroke;
        patternPaint.StrokeWidth = 2;

        var rect = new SKRect(x, y, x + CardWidth, y + CardHeight);
        var inner = rect;
        inner.Inflate(-7, -7);
        canvas.DrawRoundRect(new SKRoundRect(rect, 12, 12), backPaint);
        canvas.DrawRoundRect(new SKRoundRect(inner, 8, 8), patternPaint);
        canvas.DrawRoundRect(new SKRoundRect(rect, 12, 12), borderPaint);
    }

    private static string Truncate(string value, int maxChars)
    {
        if (string.IsNullOrWhiteSpace(value)) return "Player";
        return value.Length <= maxChars ? value : value[..(maxChars - 1)] + "…";
    }

    private static string PhaseName(PokerPhase phase, ILocalizer localizer) => phase switch
    {
        PokerPhase.PreFlop => localizer.Get("poker", "phase.preflop"),
        PokerPhase.Flop => localizer.Get("poker", "phase.flop"),
        PokerPhase.Turn => localizer.Get("poker", "phase.turn"),
        PokerPhase.River => localizer.Get("poker", "phase.river"),
        PokerPhase.Showdown => localizer.Get("poker", "phase.showdown"),
        _ => localizer.Get("poker", "phase.seating"),
    };
}
