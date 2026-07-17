using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using NinjaTrader.Cbi;
using NinjaTrader.Core.FloatingPoint;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.DrawingTools;

namespace NinjaTrader.NinjaScript.Strategies
{
	public class SfourmAutoEntry : Strategy
	{
		private readonly HashSet<string> processedTags = new HashSet<string>();

		[NinjaScriptProperty]
		[Display(Name = "Tag del Sfourm (vacío = cualquiera)", Order = 1, GroupName = "Parameters")]
		public string DrawingToolTag { get; set; }

		[Range(1, int.MaxValue)]
		[NinjaScriptProperty]
		[Display(Name = "Máximo de contratos", Order = 2, GroupName = "Parameters")]
		public int MaxContracts { get; set; }

		[NinjaScriptProperty]
		[Display(Name = "Usar niveles parciales (TP escalonados)", Order = 3, GroupName = "Parameters")]
		public bool UsePartialLevels { get; set; }

		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description					= "Toma una entrada (o varias parciales) usando Entry/Risk/Reward y el StopLoss($) definidos en una herramienta Sfourm dibujada en el gráfico.";
				Name						= "SfourmAutoEntry";
				Calculate					= Calculate.OnEachTick;
				EntriesPerDirection			= 20;
				EntryHandling				= EntryHandling.UniqueEntries;
				IsExitOnSessionCloseStrategy = true;
				ExitOnSessionCloseSeconds	= 30;
				StartBehavior				= StartBehavior.WaitUntilFlat;
				TimeInForce					= TimeInForce.Day;
				RealtimeErrorHandling		= RealtimeErrorHandling.StopCancelClose;
				StopTargetHandling			= StopTargetHandling.PerEntryExecution;
				BarsRequiredToTrade			= 0;
				IsInstantiatedOnEachOptimizationIteration = true;
				DrawingToolTag				= string.Empty;
				MaxContracts				= 100;
				UsePartialLevels			= true;
			}
		}

		private bool loggedChartControlWarning = false;
		private bool loggedRealtimeStart		= false;

		protected override void OnBarUpdate()
		{
			if (BarsInProgress != 0)
				return;

			if (State != State.Realtime)
				return;

			if (!loggedRealtimeStart)
			{
				Print(string.Format("{0}: strategy en tiempo real. ChartControl {1}.",
					Name, ChartControl == null ? "es NULL (agrégala desde el CHART, clic derecho > Strategies, no desde el Control Center)" : "OK"));
				loggedRealtimeStart = true;
			}

			if (Position.MarketPosition != MarketPosition.Flat)
				return;

			if (ChartControl == null)
			{
				if (!loggedChartControlWarning)
				{
					Print(string.Format("{0}: ChartControl es NULL, no se puede leer el Sfourm. Debes agregar esta strategy desde el chart (clic derecho > Strategies), no desde la pestaña Strategies del Control Center.", Name));
					loggedChartControlWarning = true;
				}
				return;
			}

			bool foundAnySfourm = false;

			foreach (DrawingTool draw in DrawObjects.ToList())
			{
				Sfourm sfourm = draw as Sfourm;
				if (sfourm == null)
					continue;

				foundAnySfourm = true;

				if (!string.IsNullOrEmpty(DrawingToolTag) && sfourm.Tag != DrawingToolTag)
					continue;

				if (processedTags.Contains(sfourm.Tag))
					continue;

				if (sfourm.EntryAnchor.IsEditing || sfourm.RiskAnchor.IsEditing || sfourm.RewardAnchor.IsEditing)
					continue;

				SubmitFromDrawingTool(sfourm);
			}

			if (!foundAnySfourm && CurrentBar % 200 == 0)
				Print(string.Format("{0}: no se encontró ningún Sfourm dibujado en este chart todavía.", Name));
		}

		private void SubmitFromDrawingTool(Sfourm sfourm)
		{
			double entryPrice	= Instrument.MasterInstrument.RoundToTickSize(sfourm.EntryAnchor.Price);
			double stopPrice	= Instrument.MasterInstrument.RoundToTickSize(sfourm.RiskAnchor.Price);
			double targetPrice	= Instrument.MasterInstrument.RoundToTickSize(sfourm.RewardAnchor.Price);
			double pointValue	= Instrument.MasterInstrument.PointValue;

			double denom = Math.Abs((entryPrice - stopPrice) * pointValue);
			if (denom.ApproxCompare(0) == 0)
			{
				Print(string.Format("{0}: entry y stop están al mismo precio, no se pudo calcular la cantidad.", sfourm.Tag));
				return;
			}

			int totalQuantity = (int)Math.Floor(Math.Abs(sfourm.StopLoss / denom));
			totalQuantity = Math.Max(1, Math.Min(totalQuantity, MaxContracts));

			bool isLong = stopPrice < entryPrice;

			int levels = UsePartialLevels ? Math.Max((int)Math.Round(sfourm.Ratio), 1) : 1;

			int baseQty		= totalQuantity / levels;
			int remainder	= totalQuantity % levels;

			for (int i = 1; i <= levels; i++)
			{
				int levelQty = baseQty + (i <= remainder ? 1 : 0);
				if (levelQty <= 0)
					continue;

				double fraction	 = (double)i / levels;
				double levelPrice = levels == 1
					? targetPrice
					: Instrument.MasterInstrument.RoundToTickSize(entryPrice + fraction * (targetPrice - entryPrice));

				string signalName = string.Format("Sfourm_{0}_L{1}", sfourm.Tag, i);

				SetStopLoss(signalName, CalculationMode.Price, stopPrice, false);
				SetProfitTarget(signalName, CalculationMode.Price, levelPrice);

				if (isLong)
					EnterLongLimit(0, true, levelQty, entryPrice, signalName);
				else
					EnterShortLimit(0, true, levelQty, entryPrice, signalName);

				Print(string.Format("{0} [{1}/{2}]: {3} enviado. qty={4} entry={5} stop={6} target={7}",
					sfourm.Tag, i, levels, isLong ? "LONG" : "SHORT", levelQty, entryPrice, stopPrice, levelPrice));
			}

			processedTags.Add(sfourm.Tag);
		}
	}
}
