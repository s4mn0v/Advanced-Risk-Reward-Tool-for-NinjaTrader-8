# Advanced Risk/Reward Tool for NinjaTrader 8

advance-risk-reward is an enhanced Risk/Reward drawing tool that extends the native NinjaTrader utility. It is designed for traders who require precise position sizing and automation by integrating financial risk management directly into the chart interface.

### Overview

Unlike standard drawing tools, advance-risk-reward calculates the exact number of contracts or shares needed based on a user-defined dollar risk. It also synchronizes this calculation with the NinjaTrader ChartTrader for faster execution.

### Key Features

*   **Automated Position Sizing**: Calculates quantity based on a fixed "StopLoss (USD)" input and the distance between entry and stop levels.
*   **ChartTrader Integration**: Automatically fills the quantity selector in ChartTrader with the calculated position size.
*   **Partial Reward Levels**: Visualizes incremental target levels (1:1, 1:2, etc.) between the entry and the final target.
*   **Safety Limits**: Includes a "Max Contracts" setting to prevent the tool from setting quantities above a specific threshold.
*   **Multiple Display Units**: View risk and reward data in Currency, Ticks, Pips, or Percentage.
*   **Visual Customization**: Configurable background fills and strokes for stop and target zones.

### Installation

1. Close NinjaTrader 8.
2. Copy the **advance-risk-reward.cs** file into the following directory:
   `C:\Users\<YourUsername>\Documents\NinjaTrader 8\bin\Custom\DrawingTools`
3. Restart NinjaTrader 8.
4. The tool will appear in the Drawing Tools menu (default shortcut: F12) under the name **advance-risk-reward**.

### Usage and Settings

After drawing the tool on a chart by selecting the entry and stop points, you can modify its behavior in the Properties window:

*   **Ratio**: Sets the primary target distance relative to your risk.
*   **StopLoss (USD)**: Enter the maximum dollar amount you wish to risk per trade.
*   **Max Contracts**: Sets a ceiling for the quantity sent to ChartTrader.
*   **Modificar Cant Contratos**: Toggle this on to enable the automatic ChartTrader quantity update.
*   **Mostrar RRs**: Toggle this to show or hide intermediate 1:x reward levels.
*   **Display Unit**: Choose how price and risk information are formatted in the labels.

### Important Note

Any trades or investments made using this tool are at your own risk. Ensure you test the quantity auto-fill behavior on a simulated account before using it in a live environment.

---

AutoEntry (Strategy)

SfourmAutoEntry is a companion strategy that automates the execution of trades based on the Sfourm drawing tool. It converts visual analysis into active orders by monitoring the chart for specific drawings and submitting them to the market.
Overview

The strategy detects when a Sfourm object is placed on the chart. It reads the entry, stop, and reward prices, calculates the appropriate position size based on the tool's internal "StopLoss (USD)" setting, and manages the entry and exit orders automatically.
Key Features

- Automated Order Submission: Sends limit orders at the entry price as soon as the drawing is finalized.
- Dynamic Scaling (Scaling Out): If "Partial Levels" is enabled, the strategy splits the total position into multiple Take Profit orders distributed across the R:R levels.
- Risk Synchronization: Automatically applies the calculated Stop Loss and Profit Targets to each execution.
- Tag Filtering: Can be configured to trade only specific drawings via the "Tag" parameter or handle every Sfourm object found on the chart.
- Precision Management: Ensures the tool is not in "Editing" mode before triggering to avoid entries based on accidental movements.

Parameters

- Tag del Sfourm: Filter to target specific drawings. Leave empty to process any Sfourm tool.
- Máximo de contratos: A secondary safety ceiling for the strategy's total exposure.
- Usar niveles parciales: Determines if the strategy scaling out into multiple targets or uses a single final target.

Installation

Copy the SfourmAutoEntry.cs file into:
C:\Users\<YourUsername>\Documents\NinjaTrader 8\bin\Custom\Strategies

To use it, right-click your chart, select Strategies, and add SfourmAutoEntry. Ensure "Enabled" is checked.
