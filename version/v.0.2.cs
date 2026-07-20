#region Using declarations

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using NinjaTrader.Cbi;
using NinjaTrader.Core.FloatingPoint;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Gui.Tools;

#endregion

namespace NinjaTrader.NinjaScript.DrawingTools
{
    public class Sfourm : DrawingTool
    {
        private const int cursorSensitivity = 15;
        private ChartAnchor editingAnchor;
        private double entryPrice;
        private bool needsRatioUpdate = true;
        private double ratio = 2;
        private double risk;
        private double reward;
        private double stopPrice;
        private double targetPrice;
        private double textleftPoint;
        private double textRightPoint;
        private double contracts;
        private double lastAppliedContracts = double.MinValue;

        private NinjaTrader.Gui.Tools.QuantityUpDown quantitySelector;

        [Browsable(false)]
        private bool DrawTarget { get { return (RiskAnchor != null && !RiskAnchor.IsEditing) || (RewardAnchor != null && !RewardAnchor.IsEditing); } }

        [Display(Order = 1)]
        public ChartAnchor EntryAnchor { get; set; }
        [Display(Order = 2)]
        public ChartAnchor RiskAnchor { get; set; }
        [Browsable(false)]
        public ChartAnchor RewardAnchor { get; set; }

        public override object Icon { get { return Icons.DrawRiskReward; } }

        [Range(0, double.MaxValue)]
        [NinjaScriptProperty]
        [Display(ResourceType = typeof(Custom.Resource), Name = "NinjaScriptDrawingToolRiskRewardRatio", GroupName = "NinjaScriptGeneral", Order = 1)]
        public double Ratio
        {
            get { return ratio; }
            set
            {
                if (ratio.ApproxCompare(value) == 0)
                    return;
                ratio = value;
                needsRatioUpdate = true;
            }
        }

        [Display(ResourceType = typeof(Custom.Resource), Name = "NinjaScriptDrawingToolAnchor", GroupName = "NinjaScriptLines", Order = 3)]
        public Stroke AnchorLineStroke { get; set; }
        [Display(ResourceType = typeof(Custom.Resource), Name = "NinjaScriptDrawingToolRiskRewardLineStrokeEntry", GroupName = "NinjaScriptLines", Order = 6)]
        public Stroke EntryLineStroke { get; set; }
        [Display(ResourceType = typeof(Custom.Resource), Name = "NinjaScriptDrawingToolRiskRewardLineStrokeRisk", GroupName = "NinjaScriptLines", Order = 4)]
        public Stroke StopLineStroke { get; set; }
        [Display(ResourceType = typeof(Custom.Resource), Name = "NinjaScriptDrawingToolRiskRewardLineStrokeReward", GroupName = "NinjaScriptLines", Order = 5)]
        public Stroke TargetLineStroke { get; set; }

        [Display(ResourceType = typeof(Custom.Resource), Name = "Fondo del Stop", GroupName = "NinjaScriptLines", Order = 6)]
        public Stroke StopLineStrokeBack { get; set; }
        [Display(ResourceType = typeof(Custom.Resource), Name = "Fondo del Target", GroupName = "NinjaScriptLines", Order = 7)]
        public Stroke TargetLineStrokeBack { get; set; }

        [Display(ResourceType = typeof(Custom.Resource), Name = "Stop Real (posicion ejecutada)", GroupName = "NinjaScriptLines", Order = 8)]
        public Stroke StopLineStrokeReal { get; set; }
        [Display(ResourceType = typeof(Custom.Resource), Name = "Target Real (posicion ejecutada)", GroupName = "NinjaScriptLines", Order = 9)]
        public Stroke TargetLineStrokeReal { get; set; }

        public override IEnumerable<ChartAnchor> Anchors { get { return new[] { EntryAnchor, RiskAnchor, RewardAnchor }; } }

        [Display(ResourceType = typeof(Custom.Resource), Name = "NinjaScriptDrawingToolFibonacciRetracementsExtendLinesRight", GroupName = "NinjaScriptLines", Order = 2)]
        public bool IsExtendedLinesRight { get; set; }
        [Display(ResourceType = typeof(Custom.Resource), Name = "NinjaScriptDrawingToolFibonacciRetracementsExtendLinesLeft", GroupName = "NinjaScriptLines", Order = 1)]
        public bool IsExtendedLinesLeft { get; set; }
        [Display(ResourceType = typeof(Custom.Resource), Name = "NinjaScriptDrawingToolTextAlignment", GroupName = "NinjaScriptGeneral", Order = 2)]
        public TextLocation TextAlignment { get; set; }
        [Display(ResourceType = typeof(Custom.Resource), Name = "NinjaScriptDrawingToolRulerYValueDisplayUnit", GroupName = "NinjaScriptGeneral", Order = 3)]
        public ValueUnit DisplayUnit { get; set; }

        [Display(ResourceType = typeof(Custom.Resource), Name = "StopLoss(USD)", GroupName = "NinjaScriptGeneral", Order = 0)]
        public double StopLoss { get; set; }

        [Display(ResourceType = typeof(Custom.Resource), Name = "Mostrar RRs(1:x)", GroupName = "NinjaScriptGeneral", Order = 0)]
        public bool ShowPartialLevels { get; set; }

        [Display(ResourceType = typeof(Custom.Resource), Name = "Modificar Cant Contratos", GroupName = "NinjaScriptGeneral", Order = 0)]
        public bool modificarContratosFlag { get; set; }

        [Range(1, int.MaxValue)]
        [Display(ResourceType = typeof(Custom.Resource), Name = "Max Contratos", GroupName = "NinjaScriptGeneral", Order = 0)]
        public int MaxContracts { get; set; }

        [Display(ResourceType = typeof(Custom.Resource), Name = "Mostrar P&L Real de Posicion Ejecutada", GroupName = "NinjaScriptGeneral", Order = 0)]
        public bool ShowRealPositionPnL { get; set; }

        public override bool SupportsAlerts { get { return true; } }

        private Position GetOpenPosition()
        {
            if (!ShowRealPositionPnL || AttachedTo == null || AttachedTo.Instrument == null)
                return null;

            try
            {
                return Account.All
                    .SelectMany(a => a.Positions)
                    .FirstOrDefault(p => p.Instrument == AttachedTo.Instrument && p.MarketPosition != MarketPosition.Flat);
            }
            catch
            {
                return null;
            }
        }

        private void DrawPriceText(ChartAnchor anchor, Point point, double price, ChartControl chartControl, ChartPanel chartPanel, ChartScale chartScale)
        {
            if (TextAlignment == TextLocation.Off)
                return;

            string priceString;
            ChartBars chartBars = GetAttachedToChartBars();

            if (chartBars == null)
                return;

            if (!IsUserDrawn)
                price = AttachedTo.Instrument.MasterInstrument.RoundToTickSize(anchor.Price);

            priceString = GetPriceString(price, chartBars);

            Stroke color;
            textleftPoint = RiskAnchor.GetPoint(chartControl, chartPanel, chartScale).X;
            textRightPoint = EntryAnchor.GetPoint(chartControl, chartPanel, chartScale).X;

            bool hasRealPosition = GetOpenPosition() != null;

            if (anchor == RewardAnchor) color = hasRealPosition ? TargetLineStrokeReal : TargetLineStroke;
            else if (anchor == RiskAnchor) color = hasRealPosition ? StopLineStrokeReal : StopLineStroke;
            else if (anchor == EntryAnchor) color = EntryLineStroke;
            else color = AnchorLineStroke;

            SimpleFont wpfFont = chartControl.Properties.LabelFont ?? new SimpleFont();
            SharpDX.DirectWrite.TextFormat textFormat = wpfFont.ToDirectWriteTextFormat();
            textFormat.TextAlignment = SharpDX.DirectWrite.TextAlignment.Leading;
            textFormat.WordWrapping = SharpDX.DirectWrite.WordWrapping.NoWrap;
            SharpDX.DirectWrite.TextLayout textLayout = new SharpDX.DirectWrite.TextLayout(Core.Globals.DirectWriteFactory, priceString, textFormat, chartPanel.H, textFormat.FontSize);

            if (RiskAnchor.Time <= EntryAnchor.Time)
            {
                if (!IsExtendedLinesLeft && !IsExtendedLinesRight)
                    switch (TextAlignment)
                    {
                        case TextLocation.InsideLeft: point.X = textleftPoint; break;
                        case TextLocation.InsideRight: point.X = textRightPoint - textLayout.Metrics.Width; break;
                        case TextLocation.ExtremeLeft: point.X = textleftPoint; break;
                        case TextLocation.ExtremeRight: point.X = textRightPoint - textLayout.Metrics.Width; break;
                    }
                else if (IsExtendedLinesLeft && !IsExtendedLinesRight)
                    switch (TextAlignment)
                    {
                        case TextLocation.InsideLeft: point.X = textleftPoint; break;
                        case TextLocation.InsideRight: point.X = textRightPoint - textLayout.Metrics.Width; break;
                        case TextLocation.ExtremeLeft: point.X = chartPanel.X; break;
                        case TextLocation.ExtremeRight: point.X = textRightPoint - textLayout.Metrics.Width; break;
                    }
                else if (!IsExtendedLinesLeft && IsExtendedLinesRight)
                    switch (TextAlignment)
                    {
                        case TextLocation.InsideLeft: point.X = textleftPoint; break;
                        case TextLocation.InsideRight: point.X = textRightPoint - textLayout.Metrics.Width; break;
                        case TextLocation.ExtremeLeft: point.X = textleftPoint; break;
                        case TextLocation.ExtremeRight: point.X = chartPanel.W - textLayout.Metrics.Width; break;
                    }
                else if (IsExtendedLinesLeft && IsExtendedLinesRight)
                    switch (TextAlignment)
                    {
                        case TextLocation.InsideLeft: point.X = textleftPoint; break;
                        case TextLocation.InsideRight: point.X = textRightPoint - textLayout.Metrics.Width; break;
                        case TextLocation.ExtremeRight: point.X = chartPanel.W - textLayout.Metrics.Width; break;
                        case TextLocation.ExtremeLeft: point.X = chartPanel.X; break;
                    }
            }
            else if (RiskAnchor.Time >= EntryAnchor.Time)
                if (!IsExtendedLinesLeft && !IsExtendedLinesRight)
                {
                    switch (TextAlignment)
                    {
                        case TextLocation.InsideLeft: point.X = textRightPoint; break;
                        case TextLocation.InsideRight: point.X = textleftPoint - textLayout.Metrics.Width; break;
                        case TextLocation.ExtremeLeft: point.X = textRightPoint; break;
                        case TextLocation.ExtremeRight: point.X = textleftPoint - textLayout.Metrics.Width; break;
                    }
                }
                else if (IsExtendedLinesLeft && !IsExtendedLinesRight)
                    switch (TextAlignment)
                    {
                        case TextLocation.InsideLeft: point.X = textRightPoint; break;
                        case TextLocation.InsideRight: point.X = textleftPoint - textLayout.Metrics.Width; break;
                        case TextLocation.ExtremeLeft: point.X = chartPanel.X; break;
                        case TextLocation.ExtremeRight: point.X = textleftPoint - textLayout.Metrics.Width; break;
                    }
                else if (!IsExtendedLinesLeft && IsExtendedLinesRight)
                    switch (TextAlignment)
                    {
                        case TextLocation.InsideLeft: point.X = textRightPoint; break;
                        case TextLocation.InsideRight: point.X = textleftPoint - textLayout.Metrics.Width; break;
                        case TextLocation.ExtremeLeft: point.X = textRightPoint; break;
                        case TextLocation.ExtremeRight: point.X = chartPanel.W - textLayout.Metrics.Width; break;
                    }
                else if (IsExtendedLinesLeft && IsExtendedLinesRight)
                    switch (TextAlignment)
                    {
                        case TextLocation.InsideLeft: point.X = textRightPoint; break;
                        case TextLocation.InsideRight: point.X = textleftPoint - textLayout.Metrics.Width; break;
                        case TextLocation.ExtremeRight: point.X = chartPanel.W - textLayout.Metrics.Width; break;
                        case TextLocation.ExtremeLeft: point.X = chartPanel.X; break;
                    }

            RenderTarget.DrawTextLayout(new SharpDX.Vector2((float)point.X, (float)point.Y), textLayout, color.BrushDX, SharpDX.Direct2D1.DrawTextOptions.NoSnap);
        }

        private void DrawPriceTextPartials(ChartAnchor anchor, Point point, double price, ChartControl chartControl, ChartPanel chartPanel, ChartScale chartScale, int numero)
        {
            if (TextAlignment == TextLocation.Off)
                return;

            string priceString;
            ChartBars chartBars = GetAttachedToChartBars();

            if (chartBars == null)
                return;

            if (!IsUserDrawn)
                price = AttachedTo.Instrument.MasterInstrument.RoundToTickSize(anchor.Price);

            priceString = GetPriceStringPartials(price, chartBars, numero);

            Stroke color;
            textleftPoint = RiskAnchor.GetPoint(chartControl, chartPanel, chartScale).X;
            textRightPoint = EntryAnchor.GetPoint(chartControl, chartPanel, chartScale).X;

            bool hasRealPosition = GetOpenPosition() != null;

            if (anchor == RewardAnchor) color = hasRealPosition ? TargetLineStrokeReal : TargetLineStroke;
            else if (anchor == RiskAnchor) color = hasRealPosition ? StopLineStrokeReal : StopLineStroke;
            else if (anchor == EntryAnchor) color = EntryLineStroke;
            else color = AnchorLineStroke;

            SimpleFont wpfFont = chartControl.Properties.LabelFont ?? new SimpleFont();
            SharpDX.DirectWrite.TextFormat textFormat = wpfFont.ToDirectWriteTextFormat();
            textFormat.TextAlignment = SharpDX.DirectWrite.TextAlignment.Leading;
            textFormat.WordWrapping = SharpDX.DirectWrite.WordWrapping.NoWrap;
            SharpDX.DirectWrite.TextLayout textLayout = new SharpDX.DirectWrite.TextLayout(Core.Globals.DirectWriteFactory, priceString, textFormat, chartPanel.H, textFormat.FontSize);

            if (RiskAnchor.Time <= EntryAnchor.Time)
            {
                if (!IsExtendedLinesLeft && !IsExtendedLinesRight)
                    switch (TextAlignment)
                    {
                        case TextLocation.InsideLeft: point.X = textleftPoint; break;
                        case TextLocation.InsideRight: point.X = textRightPoint - textLayout.Metrics.Width; break;
                        case TextLocation.ExtremeLeft: point.X = textleftPoint; break;
                        case TextLocation.ExtremeRight: point.X = textRightPoint - textLayout.Metrics.Width; break;
                    }
                else if (IsExtendedLinesLeft && !IsExtendedLinesRight)
                    switch (TextAlignment)
                    {
                        case TextLocation.InsideLeft: point.X = textleftPoint; break;
                        case TextLocation.InsideRight: point.X = textRightPoint - textLayout.Metrics.Width; break;
                        case TextLocation.ExtremeLeft: point.X = chartPanel.X; break;
                        case TextLocation.ExtremeRight: point.X = textRightPoint - textLayout.Metrics.Width; break;
                    }
                else if (!IsExtendedLinesLeft && IsExtendedLinesRight)
                    switch (TextAlignment)
                    {
                        case TextLocation.InsideLeft: point.X = textleftPoint; break;
                        case TextLocation.InsideRight: point.X = textRightPoint - textLayout.Metrics.Width; break;
                        case TextLocation.ExtremeLeft: point.X = textleftPoint; break;
                        case TextLocation.ExtremeRight: point.X = chartPanel.W - textLayout.Metrics.Width; break;
                    }
                else if (IsExtendedLinesLeft && IsExtendedLinesRight)
                    switch (TextAlignment)
                    {
                        case TextLocation.InsideLeft: point.X = textleftPoint; break;
                        case TextLocation.InsideRight: point.X = textRightPoint - textLayout.Metrics.Width; break;
                        case TextLocation.ExtremeRight: point.X = chartPanel.W - textLayout.Metrics.Width; break;
                        case TextLocation.ExtremeLeft: point.X = chartPanel.X; break;
                    }
            }
            else if (RiskAnchor.Time >= EntryAnchor.Time)
                if (!IsExtendedLinesLeft && !IsExtendedLinesRight)
                {
                    switch (TextAlignment)
                    {
                        case TextLocation.InsideLeft: point.X = textRightPoint; break;
                        case TextLocation.InsideRight: point.X = textleftPoint - textLayout.Metrics.Width; break;
                        case TextLocation.ExtremeLeft: point.X = textRightPoint; break;
                        case TextLocation.ExtremeRight: point.X = textleftPoint - textLayout.Metrics.Width; break;
                    }
                }
                else if (IsExtendedLinesLeft && !IsExtendedLinesRight)
                    switch (TextAlignment)
                    {
                        case TextLocation.InsideLeft: point.X = textRightPoint; break;
                        case TextLocation.InsideRight: point.X = textleftPoint - textLayout.Metrics.Width; break;
                        case TextLocation.ExtremeLeft: point.X = chartPanel.X; break;
                        case TextLocation.ExtremeRight: point.X = textleftPoint - textLayout.Metrics.Width; break;
                    }
                else if (!IsExtendedLinesLeft && IsExtendedLinesRight)
                    switch (TextAlignment)
                    {
                        case TextLocation.InsideLeft: point.X = textRightPoint; break;
                        case TextLocation.InsideRight: point.X = textleftPoint - textLayout.Metrics.Width; break;
                        case TextLocation.ExtremeLeft: point.X = textRightPoint; break;
                        case TextLocation.ExtremeRight: point.X = chartPanel.W - textLayout.Metrics.Width; break;
                    }
                else if (IsExtendedLinesLeft && IsExtendedLinesRight)
                    switch (TextAlignment)
                    {
                        case TextLocation.InsideLeft: point.X = textRightPoint; break;
                        case TextLocation.InsideRight: point.X = textleftPoint - textLayout.Metrics.Width; break;
                        case TextLocation.ExtremeRight: point.X = chartPanel.W - textLayout.Metrics.Width; break;
                        case TextLocation.ExtremeLeft: point.X = chartPanel.X; break;
                    }

            RenderTarget.DrawTextLayout(new SharpDX.Vector2((float)point.X, (float)point.Y), textLayout, color.BrushDX, SharpDX.Direct2D1.DrawTextOptions.NoSnap);
        }

        public override IEnumerable<AlertConditionItem> GetAlertConditionItems()
        {
            return Anchors.Select(anchor => new AlertConditionItem
            {
                Name = anchor.DisplayName,
                ShouldOnlyDisplayName = true,
                Tag = anchor
            });
        }

        public override Cursor GetCursor(ChartControl chartControl, ChartPanel chartPanel, ChartScale chartScale, Point point)
        {
            switch (DrawingState)
            {
                case DrawingState.Building: return Cursors.Pen;
                case DrawingState.Moving: return IsLocked ? Cursors.No : Cursors.SizeAll;
                case DrawingState.Editing: return IsLocked ? Cursors.No : (editingAnchor == EntryAnchor ? Cursors.SizeNESW : Cursors.SizeNWSE);
                default:
                    Point entryAnchorPixelPoint = EntryAnchor.GetPoint(chartControl, chartPanel, chartScale);

                    ChartAnchor closest = GetClosestAnchor(chartControl, chartPanel, chartScale, cursorSensitivity, point);

                    if (closest != null)
                        return IsLocked ? Cursors.Arrow : (closest == EntryAnchor ? Cursors.SizeNESW : Cursors.SizeNWSE);

                    Point stopAnchorPixelPoint = RiskAnchor.GetPoint(chartControl, chartPanel, chartScale);
                    Vector anchorsVector = stopAnchorPixelPoint - entryAnchorPixelPoint;

                    if (MathHelper.IsPointAlongVector(point, entryAnchorPixelPoint, anchorsVector, cursorSensitivity))
                        return IsLocked ? Cursors.Arrow : Cursors.SizeAll;

                    if (!DrawTarget)
                        return null;

                    Point targetPoint = RewardAnchor.GetPoint(chartControl, chartPanel, chartScale);
                    Vector targetToEntryVector = targetPoint - entryAnchorPixelPoint;
                    return MathHelper.IsPointAlongVector(point, entryAnchorPixelPoint, targetToEntryVector, cursorSensitivity) ? (IsLocked ? Cursors.Arrow : Cursors.SizeAll) : null;
            }
        }

        private double CalculateContracts(double price, double yValueEntry, double pointValue)
        {
            double denom = (price - yValueEntry) * pointValue;
            return denom.ApproxCompare(0) == 0 ? 0 : Math.Round(Math.Abs(StopLoss / denom), 2);
        }

        private string GetPriceString(double price, ChartBars chartBars)
        {
            string priceString;
            double yValueEntry = AttachedTo.Instrument.MasterInstrument.RoundToTickSize(EntryAnchor.Price);
            double tickSize = AttachedTo.Instrument.MasterInstrument.TickSize;
            double pointValue = AttachedTo.Instrument.MasterInstrument.PointValue;
            double pct = price > yValueEntry ? 1 : price == yValueEntry ? 0 : -1;

            contracts = CalculateContracts(price, yValueEntry, pointValue);

            switch (DisplayUnit)
            {
                case ValueUnit.Currency:
                    if (AttachedTo.Instrument.MasterInstrument.InstrumentType == InstrumentType.Forex)
                    {
                        priceString = price > yValueEntry ?
                            Core.Globals.FormatCurrency(AttachedTo.Instrument.MasterInstrument.RoundToTickSize(price - yValueEntry) / tickSize * (tickSize * pointValue * Account.All[0].ForexLotSize)) :
                            Core.Globals.FormatCurrency(AttachedTo.Instrument.MasterInstrument.RoundToTickSize(yValueEntry - price) / tickSize * (tickSize * pointValue * Account.All[0].ForexLotSize));
                    }
                    else
                    {
                        priceString = price > yValueEntry ?
                            Core.Globals.FormatCurrency(AttachedTo.Instrument.MasterInstrument.RoundToTickSize(price - yValueEntry) / tickSize * (tickSize * pointValue)) :
                            Core.Globals.FormatCurrency(AttachedTo.Instrument.MasterInstrument.RoundToTickSize(yValueEntry - price) / tickSize * (tickSize * pointValue));
                    }
                    break;
                case ValueUnit.Percent:
                    priceString = price > yValueEntry ?
                        (AttachedTo.Instrument.MasterInstrument.RoundToTickSize(price - yValueEntry) / yValueEntry).ToString("P", Core.Globals.GeneralOptions.CurrentCulture) :
                        (AttachedTo.Instrument.MasterInstrument.RoundToTickSize(yValueEntry - price) / yValueEntry).ToString("P", Core.Globals.GeneralOptions.CurrentCulture);
                    break;
                case ValueUnit.Ticks:
                    priceString = price > yValueEntry ?
                        (AttachedTo.Instrument.MasterInstrument.RoundToTickSize(price - yValueEntry) / tickSize).ToString("F0") + " " + ValueUnit.Ticks.ToString() :
                        (AttachedTo.Instrument.MasterInstrument.RoundToTickSize(yValueEntry - price) / tickSize).ToString("F0") + " " + ValueUnit.Ticks.ToString();
                    break;
                case ValueUnit.Pips:
                    priceString = price > yValueEntry ?
                        (AttachedTo.Instrument.MasterInstrument.RoundToTickSize(price - yValueEntry) / tickSize / 10).ToString("F0") + " " + ValueUnit.Pips.ToString() :
                        (AttachedTo.Instrument.MasterInstrument.RoundToTickSize(yValueEntry - price) / tickSize / 10).ToString("F0") + " " + ValueUnit.Pips.ToString();
                    break;
                default:
                    priceString = "" + Math.Round(Math.Abs((((price - yValueEntry) * pointValue) / ratio)), 2);
                    break;
            }

            Position openPosition = ShowRealPositionPnL ? GetOpenPosition() : null;

            var flatFormatEnt = "{0}";
            string str;

            if (openPosition != null)
            {
                double realQty = Math.Abs(openPosition.Quantity);
                double realDollar = Math.Round(Math.Abs((price - yValueEntry) * pointValue) * realQty, 2);

                str = string.Format("SL REAL ({0} cont.): -{1}", realQty, Core.Globals.FormatCurrency(realDollar));
            }
            else
            {
                var flatFormat = "C:{0} ML:${1} R:R 1:{2} {3}{4}";
                var levelType = "SL: ";
                str = string.Format(flatFormat, contracts.ToString(), StopLoss, ratio, levelType, priceString);
            }

            if (pct == 0)
            {
                if (openPosition != null)
                {
                    double unrealized = 0;
                    try { unrealized = openPosition.GetUnrealizedProfitLoss(PerformanceUnit.Currency, price); }
                    catch { unrealized = 0; }

                    str = string.Format("ENTRY (Qty real: {0} | P&L actual: {1})",
                        Math.Abs(openPosition.Quantity), Core.Globals.FormatCurrency(unrealized));
                }
                else
                {
                    string levelType = "ENTRY";
                    str = string.Format(flatFormatEnt, levelType);
                }
            }
            return str;
        }


        private string GetPriceStringPartials(double price, ChartBars chartBars, int numero)
        {
            string priceString;
            double yValueEntry = AttachedTo.Instrument.MasterInstrument.RoundToTickSize(EntryAnchor.Price);
            double tickSize = AttachedTo.Instrument.MasterInstrument.TickSize;
            double pointValue = AttachedTo.Instrument.MasterInstrument.PointValue;
            double auxCurrency;

            switch (DisplayUnit)
            {
                case ValueUnit.Currency:
                    if (AttachedTo.Instrument.MasterInstrument.InstrumentType == InstrumentType.Forex)
                    {
                        priceString = price > yValueEntry ?
                            Core.Globals.FormatCurrency(AttachedTo.Instrument.MasterInstrument.RoundToTickSize(price - yValueEntry) / tickSize * (tickSize * pointValue * Account.All[0].ForexLotSize)) :
                            Core.Globals.FormatCurrency(AttachedTo.Instrument.MasterInstrument.RoundToTickSize(yValueEntry - price) / tickSize * (tickSize * pointValue * Account.All[0].ForexLotSize));
                    }
                    else
                    {
                        priceString = price > yValueEntry ?
                            Core.Globals.FormatCurrency(AttachedTo.Instrument.MasterInstrument.RoundToTickSize(price - yValueEntry) / tickSize * (tickSize * pointValue)) :
                            Core.Globals.FormatCurrency(AttachedTo.Instrument.MasterInstrument.RoundToTickSize(yValueEntry - price) / tickSize * (tickSize * pointValue));
                    }
                    break;
                case ValueUnit.Percent:
                    priceString = price > yValueEntry ?
                        (AttachedTo.Instrument.MasterInstrument.RoundToTickSize(price - yValueEntry) / yValueEntry).ToString("P", Core.Globals.GeneralOptions.CurrentCulture) :
                        (AttachedTo.Instrument.MasterInstrument.RoundToTickSize(yValueEntry - price) / yValueEntry).ToString("P", Core.Globals.GeneralOptions.CurrentCulture);
                    break;
                case ValueUnit.Ticks:
                    priceString = price > yValueEntry ?
                        (AttachedTo.Instrument.MasterInstrument.RoundToTickSize(price - yValueEntry) / tickSize).ToString("F0") + " " + ValueUnit.Ticks.ToString() :
                        (AttachedTo.Instrument.MasterInstrument.RoundToTickSize(yValueEntry - price) / tickSize).ToString("F0") + " " + ValueUnit.Ticks.ToString();
                    break;
                case ValueUnit.Pips:
                    priceString = price > yValueEntry ?
                        (AttachedTo.Instrument.MasterInstrument.RoundToTickSize(price - yValueEntry) / tickSize / 10).ToString("F0") + " " + ValueUnit.Pips.ToString() :
                        (AttachedTo.Instrument.MasterInstrument.RoundToTickSize(yValueEntry - price) / tickSize / 10).ToString("F0") + " " + ValueUnit.Pips.ToString();
                    break;
                default:
                    priceString = chartBars.Bars.Instrument.MasterInstrument.FormatPrice(price);
                    break;
            }

            double denom = (price - yValueEntry) * pointValue;
            auxCurrency = denom.ApproxCompare(0) == 0 || ratio.ApproxCompare(0) == 0
                ? 0
                : Math.Round(Math.Abs((denom / ratio) * numero), 2);

            Position openPosition = ShowRealPositionPnL ? GetOpenPosition() : null;
            string str;

            if (openPosition != null)
            {
                double realQty = Math.Abs(openPosition.Quantity);
                double realDollar = Math.Round(auxCurrency * realQty, 2);

                str = string.Format("TP 1:{0} REAL ({1} cont.): +{2}", numero, realQty, Core.Globals.FormatCurrency(realDollar));
            }
            else
            {
                var mainFormat = "RR {1}:{2} {0}{3}";
                var levelType = "TP: $";
                str = string.Format(mainFormat, levelType, "1", numero, auxCurrency);
            }

            return str;
        }

        public override Point[] GetSelectionPoints(ChartControl chartControl, ChartScale chartScale)
        {
            ChartPanel chartPanel = chartControl.ChartPanels[chartScale.PanelIndex];
            Point entryPoint = EntryAnchor.GetPoint(chartControl, chartPanel, chartScale);
            Point stopPoint = RiskAnchor.GetPoint(chartControl, chartPanel, chartScale);

            if (!DrawTarget)
                return new[] { entryPoint, stopPoint };

            Point targetPoint = RewardAnchor.GetPoint(chartControl, chartPanel, chartScale);
            return new[] { entryPoint, stopPoint, targetPoint };
        }

        public override bool IsAlertConditionTrue(AlertConditionItem conditionItem, Condition condition, ChartAlertValue[] values, ChartControl chartControl, ChartScale chartScale)
        {
            ChartAnchor chartAnchor = conditionItem.Tag as ChartAnchor;
            if (chartAnchor == null)
                return false;

            ChartPanel chartPanel = chartControl.ChartPanels[PanelIndex];
            double alertY = chartScale.GetYByValue(chartAnchor.Price);
            Point entryPoint = EntryAnchor.GetPoint(chartControl, chartPanel, chartScale);
            Point stopPoint = RiskAnchor.GetPoint(chartControl, chartPanel, chartScale);
            Point targetPoint = RewardAnchor.GetPoint(chartControl, chartPanel, chartScale);
            double anchorMinX = DrawTarget ? new[] { entryPoint.X, stopPoint.X, targetPoint.X }.Min() : new[] { entryPoint.X, stopPoint.X }.Min();
            double anchorMaxX = DrawTarget ? new[] { entryPoint.X, stopPoint.X, targetPoint.X }.Max() : new[] { entryPoint.X, stopPoint.X }.Max();
            double lineStartX = IsExtendedLinesLeft ? chartPanel.X : anchorMinX;
            double lineEndX = IsExtendedLinesRight ? chartPanel.X + chartPanel.W : anchorMaxX;

            double firstBarX = chartControl.GetXByTime(values[0].Time);
            double firstBarY = chartScale.GetYByValue(values[0].Value);

            if (lineEndX < firstBarX)
                return false;

            Point lineStartPoint = new Point(lineStartX, alertY);
            Point lineEndPoint = new Point(lineEndX, alertY);

            Point barPoint = new Point(firstBarX, firstBarY);
            MathHelper.PointLineLocation pointLocation = MathHelper.GetPointLineLocation(lineStartPoint, lineEndPoint, barPoint);
            switch (condition)
            {
                case Condition.Greater: return pointLocation == MathHelper.PointLineLocation.LeftOrAbove;
                case Condition.GreaterEqual: return pointLocation == MathHelper.PointLineLocation.LeftOrAbove || pointLocation == MathHelper.PointLineLocation.DirectlyOnLine;
                case Condition.Less: return pointLocation == MathHelper.PointLineLocation.RightOrBelow;
                case Condition.LessEqual: return pointLocation == MathHelper.PointLineLocation.RightOrBelow || pointLocation == MathHelper.PointLineLocation.DirectlyOnLine;
                case Condition.Equals: return pointLocation == MathHelper.PointLineLocation.DirectlyOnLine;
                case Condition.NotEqual: return pointLocation != MathHelper.PointLineLocation.DirectlyOnLine;
                case Condition.CrossAbove:
                case Condition.CrossBelow:
                    Predicate<ChartAlertValue> predicate = v =>
                    {
                        double barX = chartControl.GetXByTime(v.Time);
                        double barY = chartScale.GetYByValue(v.Value);
                        Point stepBarPoint = new Point(barX, barY);
                        MathHelper.PointLineLocation ptLocation = MathHelper.GetPointLineLocation(lineStartPoint, lineEndPoint, stepBarPoint);
                        if (condition == Condition.CrossAbove)
                            return ptLocation == MathHelper.PointLineLocation.LeftOrAbove;
                        return ptLocation == MathHelper.PointLineLocation.RightOrBelow;
                    };
                    return MathHelper.DidPredicateCross(values, predicate);
            }
            return false;
        }

        public override bool IsVisibleOnChart(ChartControl chartControl, ChartScale chartScale, DateTime firstTimeOnChart, DateTime lastTimeOnChart)
        {
            return DrawingState == DrawingState.Building || Anchors.Any(a => a.Time >= firstTimeOnChart && a.Time <= lastTimeOnChart);
        }

        public override void OnCalculateMinMax()
        {
            MinValue = double.MaxValue;
            MaxValue = double.MinValue;

            if (!IsVisible)
                return;

            if (Anchors.Any(a => !a.IsEditing))
                foreach (ChartAnchor anchor in Anchors)
                {
                    if (anchor.DisplayName == RewardAnchor.DisplayName && !DrawTarget)
                        continue;

                    MinValue = Math.Min(anchor.Price, MinValue);
                    MaxValue = Math.Max(anchor.Price, MaxValue);
                }
        }

        public override void OnMouseDown(ChartControl chartControl, ChartPanel chartPanel, ChartScale chartScale, ChartAnchor dataPoint)
        {
            switch (DrawingState)
            {
                case DrawingState.Building:
                    if (EntryAnchor.IsEditing)
                    {
                        dataPoint.CopyDataValues(EntryAnchor);
                        dataPoint.CopyDataValues(RiskAnchor);
                        EntryAnchor.IsEditing = false;
                        entryPrice = AttachedTo.Instrument.MasterInstrument.RoundToTickSize(EntryAnchor.Price);
                    }
                    else if (RiskAnchor.IsEditing)
                    {
                        dataPoint.CopyDataValues(RiskAnchor);
                        RiskAnchor.IsEditing = false;
                        stopPrice = AttachedTo.Instrument.MasterInstrument.RoundToTickSize(RiskAnchor.Price);
                        SetReward();
                        RewardAnchor.Time = EntryAnchor.Time;
                        RewardAnchor.SlotIndex = EntryAnchor.SlotIndex;
                        RewardAnchor.IsEditing = false;
                    }
                    if (!EntryAnchor.IsEditing && !RiskAnchor.IsEditing && !RewardAnchor.IsEditing)
                    {
                        DrawingState = DrawingState.Normal;
                        IsSelected = false;
                    }
                    break;
                case DrawingState.Normal:
                    Point point = dataPoint.GetPoint(chartControl, chartPanel, chartScale);
                    editingAnchor = GetClosestAnchor(chartControl, chartPanel, chartScale, cursorSensitivity, point);
                    if (editingAnchor != null)
                    {
                        editingAnchor.IsEditing = true;
                        DrawingState = DrawingState.Editing;
                    }
                    else if (GetCursor(chartControl, chartPanel, chartScale, point) == null)
                        IsSelected = false;
                    else
                        DrawingState = DrawingState.Moving;
                    break;
            }
        }

        public override void OnMouseMove(ChartControl chartControl, ChartPanel chartPanel, ChartScale chartScale, ChartAnchor dataPoint)
        {
            if (IsLocked && DrawingState != DrawingState.Building || !IsVisible)
                return;

            if (DrawingState == DrawingState.Building)
            {
                if (EntryAnchor.IsEditing)
                    dataPoint.CopyDataValues(EntryAnchor);
                else if (RiskAnchor.IsEditing)
                    dataPoint.CopyDataValues(RiskAnchor);
                else if (RewardAnchor.IsEditing)
                    dataPoint.CopyDataValues(RewardAnchor);
            }
            else if (DrawingState == DrawingState.Editing && editingAnchor != null)
            {
                dataPoint.CopyDataValues(editingAnchor);
                if (editingAnchor != EntryAnchor)
                {
                    if (editingAnchor != RewardAnchor && Ratio.ApproxCompare(0) != 0)
                        SetReward();
                    else if (Ratio.ApproxCompare(0) != 0)
                        SetRisk();
                }
            }
            else if (DrawingState == DrawingState.Moving)
            {
                foreach (ChartAnchor anchor in Anchors)
                    anchor.MoveAnchor(InitialMouseDownAnchor, dataPoint, chartControl, chartPanel, chartScale, this);
            }

            entryPrice = AttachedTo.Instrument.MasterInstrument.RoundToTickSize(EntryAnchor.Price);
            stopPrice = AttachedTo.Instrument.MasterInstrument.RoundToTickSize(RiskAnchor.Price);
            targetPrice = AttachedTo.Instrument.MasterInstrument.RoundToTickSize(RewardAnchor.Price);
        }

        public override void OnMouseUp(ChartControl chartControl, ChartPanel chartPanel, ChartScale chartScale, ChartAnchor dataPoint)
        {
            if (DrawingState == DrawingState.Building)
                return;

            if (DrawingState == DrawingState.Editing || DrawingState == DrawingState.Moving)
                DrawingState = DrawingState.Normal;
            if (editingAnchor != null)
            {
                if (editingAnchor == EntryAnchor)
                {
                    SetReward();
                    if (Ratio.ApproxCompare(0) != 0)
                        SetRisk();
                }
                editingAnchor.IsEditing = false;
            }
            editingAnchor = null;
        }

        private void UpdateChartTraderQuantity(ChartControl chartControl)
        {
            if (!modificarContratosFlag || chartControl == null)
                return;

            if (GetOpenPosition() != null)
                return;

            if (contracts.ApproxCompare(lastAppliedContracts) == 0)
                return;

            lastAppliedContracts = contracts;

            chartControl.Dispatcher.InvokeAsync((Action)(() =>
            {
                Window chartWindow = Window.GetWindow(chartControl.Parent);
                if (chartWindow == null)
                    return;

                quantitySelector = chartWindow.FindFirst("ChartTraderControlQuantitySelector") as QuantityUpDown;
                if (quantitySelector != null)
                {
                    int val = Math.Min((int)Math.Floor(contracts), MaxContracts);
                    quantitySelector.Value = val > 0 ? val : 1;
                }
            }));
        }

        public override void OnRender(ChartControl chartControl, ChartScale chartScale)
        {
            if (!IsVisible)
                return;
            if (Anchors.All(a => a.IsEditing))
                return;

            if (needsRatioUpdate && DrawTarget)
                SetReward();

            ChartPanel chartPanel = chartControl.ChartPanels[PanelIndex];
            Point entryPoint = EntryAnchor.GetPoint(chartControl, chartPanel, chartScale);
            Point stopPoint = RiskAnchor.GetPoint(chartControl, chartPanel, chartScale);
            Point targetPoint = RewardAnchor.GetPoint(chartControl, chartPanel, chartScale);

            AnchorLineStroke.RenderTarget = RenderTarget;
            EntryLineStroke.RenderTarget = RenderTarget;
            StopLineStroke.RenderTarget = RenderTarget;
            TargetLineStrokeBack.RenderTarget = RenderTarget;
            StopLineStrokeBack.RenderTarget = RenderTarget;
            StopLineStrokeReal.RenderTarget = RenderTarget;
            TargetLineStrokeReal.RenderTarget = RenderTarget;

            bool hasRealPosition = GetOpenPosition() != null;
            Stroke currentStopStroke = hasRealPosition ? StopLineStrokeReal : StopLineStroke;
            Stroke currentTargetStroke = hasRealPosition ? TargetLineStrokeReal : TargetLineStroke;

            RenderTarget.AntialiasMode = SharpDX.Direct2D1.AntialiasMode.PerPrimitive;
            RenderTarget.DrawLine(entryPoint.ToVector2(), stopPoint.ToVector2(), AnchorLineStroke.BrushDX, AnchorLineStroke.Width, AnchorLineStroke.StrokeStyle);

            double anchorMinX = DrawTarget ? new[] { entryPoint.X, stopPoint.X, targetPoint.X }.Min() : new[] { entryPoint.X, stopPoint.X }.Min();
            double anchorMaxX = DrawTarget ? new[] { entryPoint.X, stopPoint.X, targetPoint.X }.Max() : new[] { entryPoint.X, stopPoint.X }.Max();
            double lineStartX = IsExtendedLinesLeft ? chartPanel.X : anchorMinX;
            double lineEndX = IsExtendedLinesRight ? chartPanel.X + chartPanel.W : anchorMaxX;

            SharpDX.Vector2 entryStartVector = new SharpDX.Vector2((float)lineStartX, (float)entryPoint.Y);
            SharpDX.Vector2 entryEndVector = new SharpDX.Vector2((float)lineEndX, (float)entryPoint.Y);
            SharpDX.Vector2 stopStartVector = new SharpDX.Vector2((float)lineStartX, (float)stopPoint.Y);
            SharpDX.Vector2 stopEndVector = new SharpDX.Vector2((float)lineEndX, (float)stopPoint.Y);

            double distance = Math.Sqrt(Math.Pow(targetPoint.X - entryPoint.X, 2) + Math.Pow(targetPoint.Y - entryPoint.Y, 2));

            int numInterpolatedPoints = Math.Max((int)ratio, 1);

            double segmentLength = distance / numInterpolatedPoints;

            List<Point> interpolatedPoints = new List<Point>();
            interpolatedPoints.Add(entryPoint);

            for (int i = 1; i < numInterpolatedPoints; i++)
            {
                double fraction = i * segmentLength / distance;

                double interpolatedX = entryPoint.X + fraction * (targetPoint.X - entryPoint.X);
                double interpolatedY = entryPoint.Y + fraction * (targetPoint.Y - entryPoint.Y);

                interpolatedPoints.Add(new Point((int)interpolatedX, (int)interpolatedY));
            }

            interpolatedPoints.Add(targetPoint);


            SharpDX.Direct2D1.Brush tmpBrush = IsInHitTest ? chartControl.SelectionBrush : AnchorLineStroke.BrushDX;
            if (DrawTarget)
            {
                AnchorLineStroke.RenderTarget = RenderTarget;
                RenderTarget.DrawLine(entryPoint.ToVector2(), targetPoint.ToVector2(), tmpBrush, AnchorLineStroke.Width, AnchorLineStroke.StrokeStyle);

                currentTargetStroke.RenderTarget = RenderTarget;
                SharpDX.Vector2 targetStartVector = new SharpDX.Vector2((float)lineStartX, (float)targetPoint.Y);
                SharpDX.Vector2 targetEndVector = new SharpDX.Vector2((float)lineEndX, (float)targetPoint.Y);

                tmpBrush = IsInHitTest ? chartControl.SelectionBrush : currentTargetStroke.BrushDX;
                RenderTarget.DrawLine(targetStartVector, targetEndVector, tmpBrush, currentTargetStroke.Width, currentTargetStroke.StrokeStyle);
                if (!ShowPartialLevels)
                {
                    DrawPriceTextPartials(RewardAnchor, targetPoint, targetPrice, chartControl, chartPanel, chartScale, (int)ratio);
                }


                tmpBrush = IsInHitTest ? chartControl.SelectionBrush : EntryLineStroke.BrushDX;
                RenderTarget.DrawLine(entryStartVector, entryEndVector, tmpBrush, EntryLineStroke.Width, EntryLineStroke.StrokeStyle);
                DrawPriceText(EntryAnchor, entryPoint, entryPrice, chartControl, chartPanel, chartScale);

                tmpBrush = IsInHitTest ? chartControl.SelectionBrush : currentStopStroke.BrushDX;
                RenderTarget.DrawLine(stopStartVector, stopEndVector, tmpBrush, currentStopStroke.Width, currentStopStroke.StrokeStyle);
                DrawPriceText(RiskAnchor, stopPoint, stopPrice, chartControl, chartPanel, chartScale);

                SharpDX.RectangleF stopRectangle = new SharpDX.RectangleF(stopStartVector.X, entryStartVector.Y, stopEndVector.X - entryStartVector.X, stopEndVector.Y - entryEndVector.Y);
                SharpDX.RectangleF targetRectangle = new SharpDX.RectangleF(targetStartVector.X, entryStartVector.Y, targetEndVector.X - entryStartVector.X, targetEndVector.Y - entryStartVector.Y);

                RenderTarget.FillRectangle(stopRectangle, StopLineStrokeBack.BrushDX);
                RenderTarget.FillRectangle(targetRectangle, TargetLineStrokeBack.BrushDX);

                if (ShowPartialLevels)
                {
                    for (int i = 1; i < interpolatedPoints.Count; i++)
                    {
                        SharpDX.Vector2 partialStartVector = new SharpDX.Vector2((float)lineStartX, (float)interpolatedPoints[i].Y);
                        SharpDX.Vector2 partialEndVector = new SharpDX.Vector2((float)lineEndX, (float)interpolatedPoints[i].Y);
                        tmpBrush = IsInHitTest ? chartControl.SelectionBrush : currentTargetStroke.BrushDX;
                        RenderTarget.DrawLine(partialStartVector, partialEndVector, tmpBrush, currentTargetStroke.Width, currentTargetStroke.StrokeStyle);
                        DrawPriceTextPartials(RewardAnchor, interpolatedPoints[i], targetPrice, chartControl, chartPanel, chartScale, i);
                    }
                }

                UpdateChartTraderQuantity(chartControl);
            }
        }

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = Custom.Resource.NinjaScriptDrawingToolRiskRewardDescription;
                Name = "Sfourm";
                Ratio = 1;
                StopLoss = 500;
                MaxContracts = 100;
                AnchorLineStroke = new Stroke(Brushes.DarkGray, DashStyleHelper.Solid, 1f, 50);
                EntryLineStroke = new Stroke(Brushes.Goldenrod, DashStyleHelper.Solid, 2f);
                StopLineStroke = new Stroke(Brushes.Crimson, DashStyleHelper.Solid, 2f);
                TargetLineStroke = new Stroke(Brushes.SeaGreen, DashStyleHelper.Solid, 2f);
                StopLineStrokeBack = new Stroke(Brushes.Crimson, DashStyleHelper.Solid, 2f, 20);
                TargetLineStrokeBack = new Stroke(Brushes.SeaGreen, DashStyleHelper.Solid, 2f, 20);
                StopLineStrokeReal = new Stroke(Brushes.Red, DashStyleHelper.Solid, 3f);
                TargetLineStrokeReal = new Stroke(Brushes.LimeGreen, DashStyleHelper.Solid, 3f);
                EntryAnchor = new ChartAnchor { IsEditing = true, DrawingTool = this };
                RiskAnchor = new ChartAnchor { IsEditing = true, DrawingTool = this };
                RewardAnchor = new ChartAnchor { IsEditing = true, DrawingTool = this };
                EntryAnchor.DisplayName = Custom.Resource.NinjaScriptDrawingToolRiskRewardAnchorEntry;
                RiskAnchor.DisplayName = Custom.Resource.NinjaScriptDrawingToolRiskRewardAnchorRisk;
                RewardAnchor.DisplayName = Custom.Resource.NinjaScriptDrawingToolRiskRewardAnchorReward;
                ShowPartialLevels = true;
                modificarContratosFlag = true;
                ShowRealPositionPnL = true;
                contracts = 0.0;
            }
            else if (State == State.Terminated)
                Dispose();
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public void SetReward()
        {
            if (Anchors == null || AttachedTo == null)
                return;

            entryPrice = AttachedTo.Instrument.MasterInstrument.RoundToTickSize(EntryAnchor.Price);
            stopPrice = AttachedTo.Instrument.MasterInstrument.RoundToTickSize(RiskAnchor.Price);
            risk = entryPrice - stopPrice;
            reward = risk * Ratio;
            targetPrice = AttachedTo.Instrument.MasterInstrument.RoundToTickSize(entryPrice + reward);

            RewardAnchor.Price = targetPrice;
            RewardAnchor.IsEditing = false;

            needsRatioUpdate = false;
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public void SetRisk()
        {
            if (Anchors == null || AttachedTo == null)
                return;

            entryPrice = AttachedTo.Instrument.MasterInstrument.RoundToTickSize(EntryAnchor.Price);
            targetPrice = AttachedTo.Instrument.MasterInstrument.RoundToTickSize(RewardAnchor.Price);

            reward = targetPrice - entryPrice;
            risk = reward / Ratio;
            stopPrice = AttachedTo.Instrument.MasterInstrument.RoundToTickSize(entryPrice - risk);

            RiskAnchor.Price = stopPrice;
            RiskAnchor.IsEditing = false;

            needsRatioUpdate = false;
        }
    }

    public static partial class Draw
    {
        private static Sfourm SfourmCore(NinjaScriptBase owner, string tag,
            bool isAutoScale,
            int entryBarsAgo, DateTime entryTime, double entryY,
            int stopBarsAgo, DateTime stopTime, double stopY,
            int targetBarsAgo, DateTime targetTime, double targetY,
            double ratio, int instrumentquantity, double dollarcentpertick, bool tickordollarcent, bool isStop, bool isGlobal, string templateName)
        {
            if (owner == null)
                throw new ArgumentException("owner");

            if (entryBarsAgo == int.MinValue && entryTime == Core.Globals.MinDate)
                throw new ArgumentException("entry value required");

            if (stopBarsAgo == int.MinValue && stopTime == Core.Globals.MinDate &&
                targetBarsAgo == int.MinValue && targetTime == Core.Globals.MinDate)
                throw new ArgumentException("a stop or target value is required");

            if (isGlobal && tag[0] != GlobalDrawingToolManager.GlobalDrawingToolTagPrefix)
                tag = string.Format("{0}{1}", GlobalDrawingToolManager.GlobalDrawingToolTagPrefix, tag);

            Sfourm sfourm = DrawingTool.GetByTagOrNew(owner, typeof(Sfourm), tag, templateName) as Sfourm;

            if (sfourm == null)
                return null;

            DrawingTool.SetDrawingToolCommonValues(sfourm, tag, isAutoScale, owner, isGlobal);

            ChartAnchor entryAnchor = DrawingTool.CreateChartAnchor(owner, entryBarsAgo, entryTime, entryY);
            ChartAnchor stopAnchor;
            ChartAnchor targetAnchor;

            sfourm.Ratio = ratio;

            if (isStop)
            {
                stopAnchor = DrawingTool.CreateChartAnchor(owner, stopBarsAgo, stopTime, stopY);
                entryAnchor.CopyDataValues(sfourm.EntryAnchor);
                entryAnchor.CopyDataValues(sfourm.RewardAnchor);
                stopAnchor.CopyDataValues(sfourm.RiskAnchor);
                sfourm.SetReward();
            }
            else
            {
                targetAnchor = DrawingTool.CreateChartAnchor(owner, targetBarsAgo, targetTime, targetY);
                entryAnchor.CopyDataValues(sfourm.EntryAnchor);
                entryAnchor.CopyDataValues(sfourm.RiskAnchor);
                targetAnchor.CopyDataValues(sfourm.RewardAnchor);
                sfourm.SetRisk();
            }

            sfourm.SetState(State.Active);
            return sfourm;
        }

        public static Sfourm Sfourm(NinjaScriptBase owner, string tag, bool isAutoScale, DateTime entryTime, double entryY, DateTime endTime, double endY, double ratio, int instrumentquantity, double dollarcentpertick, bool tickordollarcent, bool isStop)
        {
            return isStop
                ? SfourmCore(owner, tag, isAutoScale, int.MinValue, entryTime, entryY, int.MinValue, endTime, endY, 0, Core.Globals.MinDate, 0, ratio, instrumentquantity, dollarcentpertick, tickordollarcent, true, false, null)
                : SfourmCore(owner, tag, isAutoScale, int.MinValue, entryTime, entryY, 0, Core.Globals.MinDate, 0, int.MinValue, endTime, endY, ratio, instrumentquantity, dollarcentpertick, tickordollarcent, false, false, null);
        }

        public static Sfourm Sfourm(NinjaScriptBase owner, string tag, bool isAutoScale, int entryBarsAgo, double entryY, int endBarsAgo, double endY, double ratio, int instrumentquantity, double dollarcentpertick, bool tickordollarcent, bool isStop)
        {
            return isStop
                ? SfourmCore(owner, tag, isAutoScale, entryBarsAgo, Core.Globals.MinDate, entryY, endBarsAgo, Core.Globals.MinDate, endY, 0, Core.Globals.MinDate, 0, ratio, instrumentquantity, dollarcentpertick, tickordollarcent, true, false, null)
                : SfourmCore(owner, tag, isAutoScale, entryBarsAgo, Core.Globals.MinDate, entryY, 0, Core.Globals.MinDate, 0, endBarsAgo, Core.Globals.MinDate, endY, ratio, instrumentquantity, dollarcentpertick, tickordollarcent, false, false, null);
        }

        public static Sfourm Sfourm(NinjaScriptBase owner, string tag, bool isAutoScale, DateTime entryTime, double entryY, DateTime endTime, double endY, double ratio, int instrumentquantity, double dollarcentpertick, bool tickordollarcent, bool isStop, bool isGlobal, string templateName)
        {
            return isStop
                ? SfourmCore(owner, tag, isAutoScale, int.MinValue, entryTime, entryY, int.MinValue, endTime, endY, 0, Core.Globals.MinDate, 0, ratio, instrumentquantity, dollarcentpertick, tickordollarcent, true, isGlobal, templateName)
                : SfourmCore(owner, tag, isAutoScale, int.MinValue, entryTime, entryY, 0, Core.Globals.MinDate, 0, int.MinValue, endTime, endY, ratio, instrumentquantity, dollarcentpertick, tickordollarcent, false, isGlobal, templateName);
        }

        public static Sfourm Sfourm(NinjaScriptBase owner, string tag, bool isAutoScale, int entryBarsAgo, double entryY, int endBarsAgo, double endY, double ratio, int instrumentquantity, double dollarcentpertick, bool tickordollarcent, bool isStop, bool isGlobal, string templateName)
        {
            return isStop
                ? SfourmCore(owner, tag, isAutoScale, entryBarsAgo, Core.Globals.MinDate, entryY, endBarsAgo, Core.Globals.MinDate, endY, 0, Core.Globals.MinDate, 0, ratio, instrumentquantity, dollarcentpertick, tickordollarcent, true, isGlobal, templateName)
                : SfourmCore(owner, tag, isAutoScale, entryBarsAgo, Core.Globals.MinDate, entryY, 0, Core.Globals.MinDate, 0, endBarsAgo, Core.Globals.MinDate, endY, ratio, instrumentquantity, dollarcentpertick, tickordollarcent, false, isGlobal, templateName);
        }
    }

}