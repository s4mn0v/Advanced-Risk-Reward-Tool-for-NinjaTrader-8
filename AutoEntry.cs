#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NinjaTrader.Cbi;
using NinjaTrader.Core.FloatingPoint;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.DrawingTools;
#endregion

namespace NinjaTrader.NinjaScript.Strategies
{
	public class SfourmAutoEntry : Strategy
	{
		private string activeTag = null;

		private string activeSignalPrefix = null;

		private int tradeSequence = 0;

		private Grid	buttonsGrid;
		private Button	buyButton;
		private Button	sellButton;

		private bool loggedChartControlWarning = false;
		private bool loggedRealtimeStart		= false;

		[NinjaScriptProperty]
		[Display(Name = "Tag del Sfourm (vacío = el más reciente)", Order = 1, GroupName = "Parameters")]
		public string DrawingToolTag { get; set; }

		[Range(1, int.MaxValue)]
		[NinjaScriptProperty]
		[Display(Name = "Máximo de contratos", Order = 2, GroupName = "Parameters")]
		public int MaxContracts { get; set; }

		[NinjaScriptProperty]
		[Display(Name = "Usar niveles parciales (TP escalonados)", Order = 3, GroupName = "Parameters")]
		public bool UsePartialLevels { get; set; }

		[NinjaScriptProperty]
		[Display(Name = "Entrar automático al dibujar (sin botón)", Order = 4, GroupName = "Parameters")]
		public bool AutoEnter { get; set; }

		protected override void OnStateChange()
		{
			// A diferencia de antes: acá se loguea el error PERO se relanza (throw), para que
			// NinjaTrader detecte la excepción y desactive la strategy, en vez de tragársela.
			try
			{
				if (State == State.SetDefaults)
				{
					Description					= "Botones Comprar/Vender Limit (abajo a la derecha del chart) que envían una orden límite al precio del Entry del Sfourm dibujado, con el SL/TP ya pre-cargados.";
					Name						= "SfourmAutoEntry";
					Calculate					= Calculate.OnEachTick;
					EntriesPerDirection			= 20;
					EntryHandling				= EntryHandling.UniqueEntries;
					IsExitOnSessionCloseStrategy = true;
					ExitOnSessionCloseSeconds	= 30;
					StartBehavior				= StartBehavior.WaitUntilFlat;
					TimeInForce					= TimeInForce.Day;

					// Antes: RealtimeErrorHandling.IgnoreAllErrors (nunca se desactivaba sola).
					// Ahora: StopCancelClose (comportamiento estándar de NinjaTrader) -> ante un
					// error de orden/cuenta, para la strategy, cancela órdenes vivas y cierra la posición.
					RealtimeErrorHandling		= RealtimeErrorHandling.StopCancelClose;

					StopTargetHandling			= StopTargetHandling.PerEntryExecution;
					BarsRequiredToTrade			= 0;
					IsInstantiatedOnEachOptimizationIteration = true;
					DrawingToolTag				= string.Empty;
					MaxContracts				= 100;
					UsePartialLevels			= true;
					AutoEnter					= false;

					// Deja registrado en el Output CUALQUIER orden ignorada/rechazada, con el motivo.
					TraceOrders					= true;
				}
				else if (State == State.DataLoaded)
				{
					if (ChartControl != null)
						ChartControl.Dispatcher.InvokeAsync(new Action(CreateWpfControls));
				}
				else if (State == State.Terminated)
				{
					if (ChartControl != null)
						ChartControl.Dispatcher.InvokeAsync(new Action(RemoveWpfControls));
				}
			}
			catch (Exception ex)
			{
				Print(string.Format("{0}: excepción en OnStateChange ({1}): {2}", Name, State, ex.Message));
				throw;
			}
		}

		#region Botones en el chart

		private void CreateWpfControls()
		{
			try
			{
				if (ChartControl == null || buttonsGrid != null)
					return;

				buttonsGrid = new Grid
				{
					Name				= "SfourmButtonsGrid",
					HorizontalAlignment	= HorizontalAlignment.Right,
					VerticalAlignment	= VerticalAlignment.Bottom,
					Margin				= new Thickness(6)
				};
				buttonsGrid.ColumnDefinitions.Add(new ColumnDefinition());
				buttonsGrid.ColumnDefinitions.Add(new ColumnDefinition());

				buyButton = new Button
				{
					Content		= "Comprar Limit",
					Width		= 100,
					Margin		= new Thickness(2),
					Background	= Brushes.SeaGreen,
					Foreground	= Brushes.White,
					IsEnabled	= true
				};
				sellButton = new Button
				{
					Content		= "Vender Limit",
					Width		= 100,
					Margin		= new Thickness(2),
					Background	= Brushes.Crimson,
					Foreground	= Brushes.White,
					IsEnabled	= true
				};

				Grid.SetColumn(buyButton, 0);
				Grid.SetColumn(sellButton, 1);

				buyButton.Click		+= OnBuyButtonClick;
				sellButton.Click	+= OnSellButtonClick;

				buttonsGrid.Children.Add(buyButton);
				buttonsGrid.Children.Add(sellButton);

				UserControlCollection.Add(buttonsGrid);
			}
			catch (Exception ex)
			{
				Print(string.Format("{0}: excepción creando los botones: {1}", Name, ex.Message));
				throw;
			}
		}

		private void RemoveWpfControls()
		{
			try
			{
				if (buyButton != null)
					buyButton.Click -= OnBuyButtonClick;
				if (sellButton != null)
					sellButton.Click -= OnSellButtonClick;

				if (buttonsGrid != null)
					UserControlCollection.Remove(buttonsGrid);

				buttonsGrid	= null;
				buyButton	= null;
				sellButton	= null;
			}
			catch (Exception ex)
			{
				Print(string.Format("{0}: excepción removiendo los botones: {1}", Name, ex.Message));
				throw;
			}
		}

		private void OnBuyButtonClick(object sender, RoutedEventArgs e)
		{
			// Nota: este handler corre en el hilo de UI (WPF), no en el hilo del motor de la
			// strategy. Relanzar acá deja el error visible (NinjaTrader muestra el diálogo de
			// excepción no controlada), pero el mecanismo estándar de auto-desactivado de
			// NinjaTrader aplica de forma más directa a errores dentro de OnBarUpdate/OnOrderUpdate/etc.
			try
			{
				TriggerManualEntry(true);
			}
			catch (Exception ex)
			{
				Print(string.Format("{0}: excepción en click Comprar: {1}", Name, ex.Message));
				throw;
			}
		}

		private void OnSellButtonClick(object sender, RoutedEventArgs e)
		{
			try
			{
				TriggerManualEntry(false);
			}
			catch (Exception ex)
			{
				Print(string.Format("{0}: excepción en click Vender: {1}", Name, ex.Message));
				throw;
			}
		}

		#endregion

		protected override void OnBarUpdate()
		{
			try
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

				if (activeTag != null && Position.MarketPosition == MarketPosition.Flat)
				{
					Print(string.Format("{0}: watchdog detectó activeTag='{1}' con posición flat. Se libera el bloqueo.", Name, activeTag));
					activeTag			= null;
					activeSignalPrefix	= null;
				}

				if (!AutoEnter)
					return;

				if (Position.MarketPosition != MarketPosition.Flat)
					return;

				if (ChartControl == null)
				{
					if (!loggedChartControlWarning)
					{
						Print(string.Format("{0}: ChartControl es NULL, no se puede leer el Sfourm. Agrega esta strategy desde el chart (clic derecho > Strategies).", Name));
						loggedChartControlWarning = true;
					}
					return;
				}

				Sfourm sfourm = FindSfourm();
				if (sfourm == null)
					return;

				bool isLong = sfourm.RiskAnchor.Price < sfourm.EntryAnchor.Price;
				SubmitOrders(sfourm, isLong, isMarketOrder: false);
			}
			catch (Exception ex)
			{
				Print(string.Format("{0}: excepción en OnBarUpdate: {1}", Name, ex.Message));
				throw;
			}
		}

		private Sfourm FindSfourm()
		{
			try
			{
				if (ChartControl == null)
					return null;

				Sfourm best = null;

				foreach (DrawingTool draw in DrawObjects.ToList())
				{
					Sfourm sfourm = draw as Sfourm;
					if (sfourm == null)
						continue;

					if (!string.IsNullOrEmpty(DrawingToolTag) && sfourm.Tag != DrawingToolTag)
						continue;

					if (sfourm.EntryAnchor.IsEditing || sfourm.RiskAnchor.IsEditing || sfourm.RewardAnchor.IsEditing)
						continue;

					if (best == null || sfourm.EntryAnchor.Time >= best.EntryAnchor.Time)
						best = sfourm;
				}

				return best;
			}
			catch (Exception ex)
			{
				Print(string.Format("{0}: excepción en FindSfourm: {1}", Name, ex.Message));
				throw;
			}
		}

		private void TriggerManualEntry(bool wantLong)
		{
			if (Position.MarketPosition != MarketPosition.Flat)
			{
				Print(string.Format("{0}: ya hay una posición abierta, se ignora el click.", Name));
				return;
			}

			Sfourm sfourm = FindSfourm();
			if (sfourm == null)
			{
				Print(string.Format("{0}: no se encontró un Sfourm válido (terminado de dibujar) en el chart.", Name));
				return;
			}

			bool toolIsLong = sfourm.RiskAnchor.Price < sfourm.EntryAnchor.Price;
			if (toolIsLong != wantLong)
			{
				Print(string.Format("{0}: el botón presionado ({1}) no coincide con la dirección del Sfourm dibujado ({2}). No se envió ninguna orden.",
					Name, wantLong ? "Comprar" : "Vender", toolIsLong ? "Long" : "Short"));
				return;
			}

			SubmitOrders(sfourm, wantLong, isMarketOrder: false);
		}

		private void SubmitOrders(Sfourm sfourm, bool isLong, bool isMarketOrder)
		{
			try
			{
				if (activeTag != null || Position.MarketPosition != MarketPosition.Flat)
				{
					Print(string.Format("{0}: ya hay una orden/posición en curso ({1}). Esperá a que se cierre para usar los botones de nuevo.",
						Name, activeTag ?? Position.MarketPosition.ToString()));
					return;
				}

				double entryPrice	= Instrument.MasterInstrument.RoundToTickSize(sfourm.EntryAnchor.Price);
				double stopPrice	= Instrument.MasterInstrument.RoundToTickSize(sfourm.RiskAnchor.Price);
				double targetPrice	= Instrument.MasterInstrument.RoundToTickSize(sfourm.RewardAnchor.Price);
				double pointValue	= Instrument.MasterInstrument.PointValue;

				// Protección contra "limit marketable": si el Entry ya fue cruzado por el bid/ask
				// actual, un EnterLongLimit/EnterShortLimit se ejecuta de inmediato al enviarlo,
				// igual que si fuera una orden a mercado. Para que SIEMPRE sea una limit real, si
				// ya está cruzado no se envía nada y se avisa por qué.
				if (!isMarketOrder)
				{
					double currentAsk = GetCurrentAsk();
					double currentBid = GetCurrentBid();

					if (isLong && entryPrice >= currentAsk)
					{
						Print(string.Format("{0}: el Entry ({1}) ya fue alcanzado/cruzado por el ask actual ({2}). NO se envía la orden (se hubiera ejecutado como si fuera mercado). Volvé a dibujar el Entry por encima del precio actual.",
							sfourm.Tag, entryPrice, currentAsk));
						return;
					}
					if (!isLong && entryPrice <= currentBid)
					{
						Print(string.Format("{0}: el Entry ({1}) ya fue alcanzado/cruzado por el bid actual ({2}). NO se envía la orden (se hubiera ejecutado como si fuera mercado). Volvé a dibujar el Entry por debajo del precio actual.",
							sfourm.Tag, entryPrice, currentBid));
						return;
					}
				}

				double denom = Math.Abs((entryPrice - stopPrice) * pointValue);
				if (denom.ApproxCompare(0) == 0)
				{
					Print(string.Format("{0}: entry y stop están al mismo precio, no se pudo calcular la cantidad.", sfourm.Tag));
					return;
				}

				int totalQuantity = (int)Math.Floor(Math.Abs(sfourm.StopLoss / denom));
				totalQuantity = Math.Max(1, Math.Min(totalQuantity, MaxContracts));

				int levels = UsePartialLevels ? Math.Max((int)Math.Round(sfourm.Ratio), 1) : 1;

				int baseQty		= totalQuantity / levels;
				int remainder	= totalQuantity % levels;

				tradeSequence++;
				string tradePrefix = string.Format("Sfourm{0}_{1}", tradeSequence, sfourm.Tag);

				for (int i = 1; i <= levels; i++)
				{
					int levelQty = baseQty + (i <= remainder ? 1 : 0);
					if (levelQty <= 0)
						continue;

					double fraction	 = (double)i / levels;
					double levelPrice = levels == 1
						? targetPrice
						: Instrument.MasterInstrument.RoundToTickSize(entryPrice + fraction * (targetPrice - entryPrice));

					string signalName = string.Format("{0}_L{1}", tradePrefix, i);

					SetStopLoss(signalName, CalculationMode.Price, stopPrice, false);
					SetProfitTarget(signalName, CalculationMode.Price, levelPrice);

					if (isMarketOrder)
					{
						if (isLong)
							EnterLong(levelQty, signalName);
						else
							EnterShort(levelQty, signalName);
					}
					else
					{
						if (isLong)
							EnterLongLimit(0, true, levelQty, entryPrice, signalName);
						else
							EnterShortLimit(0, true, levelQty, entryPrice, signalName);
					}

					Print(string.Format("{0} [{1}/{2}]: {3} {4} enviado. qty={5} stop={6} target={7}",
						signalName, i, levels, isLong ? "LONG" : "SHORT", isMarketOrder ? "MKT" : "LIMIT",
						levelQty, stopPrice, levelPrice));
				}

				activeTag			= sfourm.Tag;
				activeSignalPrefix	= tradePrefix + "_";
			}
			catch (Exception ex)
			{
				Print(string.Format("{0}: excepción en SubmitOrders: {1}", Name, ex.Message));
				throw;
			}
		}

		protected override void OnPositionUpdate(Position position, double averagePrice, int quantity, MarketPosition marketPosition)
		{
			try
			{
				if (marketPosition == MarketPosition.Flat && activeTag != null)
				{
					Print(string.Format("{0}: posición cerrada (flat). Botones disponibles de nuevo.", Name));
					activeTag			= null;
					activeSignalPrefix	= null;
				}
			}
			catch (Exception ex)
			{
				Print(string.Format("{0}: excepción en OnPositionUpdate: {1}", Name, ex.Message));
				throw;
			}
		}

		protected override void OnOrderUpdate(Order order, double limitPrice, double stopPrice, int quantity,
			int filled, double averageFillPrice, OrderState orderState, DateTime time, ErrorCode error, string comment)
		{
			try
			{
				if (activeSignalPrefix == null || order.Name == null)
					return;

				bool isOurEntryOrder = order.Name.StartsWith(activeSignalPrefix, StringComparison.Ordinal)
					&& (order.OrderAction == OrderAction.Buy || order.OrderAction == OrderAction.SellShort);

				if (isOurEntryOrder && (orderState == OrderState.Cancelled || orderState == OrderState.Rejected)
					&& Position.MarketPosition == MarketPosition.Flat)
				{
					Print(string.Format("{0}: la entrada '{1}' fue {2}. Botones disponibles de nuevo.", Name, order.Name, orderState));
					activeTag			= null;
					activeSignalPrefix	= null;
				}
			}
			catch (Exception ex)
			{
				Print(string.Format("{0}: excepción en OnOrderUpdate: {1}", Name, ex.Message));
				throw;
			}
		}
	}
}
